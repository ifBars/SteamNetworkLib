using SteamNetworkLib.Events;
using SteamNetworkLib.Exceptions;
using SteamNetworkLib.Models;
using SteamNetworkLib.Utilities;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

namespace SteamNetworkLib.Core
{
    /// <summary>
    /// Manages Steam P2P networking for direct player-to-player communication.
    /// Provides functionality for sending messages, managing sessions, and handling P2P events.
    /// </summary>
    public class SteamP2PManager : IDisposable
    {
        private readonly SteamLobbyManager _lobbyManager;
        private NetworkRules _rules = new NetworkRules();
        private bool _disposed = false;
        private readonly Dictionary<CSteamID, DateTime> _activeSessions = new Dictionary<CSteamID, DateTime>();
        private readonly Queue<(CSteamID Target, byte[] Data, int Channel, EP2PSend SendType)> _sendQueue = new Queue<(CSteamID, byte[], int, EP2PSend)>();
        private bool _isProcessing = false;

        // Steam callbacks
        private Callback<P2PSessionRequest_t>? _sessionRequestCallback;
        private Callback<P2PSessionConnectFail_t>? _sessionConnectFailCallback;

        // Message handling
        private readonly Dictionary<string, List<Action<P2PMessage, CSteamID>>> _messageHandlers = new Dictionary<string, List<Action<P2PMessage, CSteamID>>>();

        /// <summary>
        /// Occurs when a raw P2P packet is received from another player.
        /// </summary>
        public event EventHandler<P2PPacketReceivedEventArgs>? OnPacketReceived;

        /// <summary>
        /// Occurs when a SteamNetworkLib message is received and deserialized.
        /// </summary>
        public event EventHandler<P2PMessageReceivedEventArgs>? OnMessageReceived;

        /// <summary>
        /// Occurs when another player requests a P2P session with the local player.
        /// </summary>
        public event EventHandler<P2PSessionRequestEventArgs>? OnSessionRequested;

        /// <summary>
        /// Occurs when a P2P session connection fails.
        /// </summary>
        public event EventHandler<P2PSessionConnectFailEventArgs>? OnSessionConnectFail;

        /// <summary>
        /// Occurs when a P2P packet is sent (provides send result information).
        /// </summary>
        public event EventHandler<P2PPacketSentEventArgs>? OnPacketSent;

        /// <summary>
        /// Gets a value indicating whether the P2P manager is active and ready to send/receive data.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Gets the maximum packet size in bytes that can be sent via P2P communication.
        /// </summary>
        public int MaxPacketSize => 1024 * 32; // 32KB max for larger audio/video packets

        /// <summary>
        /// Gets the number of currently active P2P sessions.
        /// </summary>
        public int ActiveSessionCount => _activeSessions.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamP2PManager"/> class.
        /// </summary>
        /// <param name="lobbyManager">The lobby manager instance to use for lobby operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when lobbyManager is null.</exception>
        /// <exception cref="SteamNetworkException">Thrown when Steam is not initialized.</exception>
        /// <summary>
        /// Initializes a new instance of the <see cref="SteamP2PManager"/> class.
        /// </summary>
        /// <param name="lobbyManager">The lobby manager instance to use for lobby operations.</param>
        {
            _lobbyManager = lobbyManager ?? throw new ArgumentNullException(nameof(lobbyManager));
            InitializeP2P();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamP2PManager"/> class with configurable network rules.
        /// </summary>
        /// <param name="lobbyManager">The lobby manager instance to use for lobby operations.</param>
        /// <param name="rules">Network rules that influence relay usage, receive channels, and session filtering.</param>
        public SteamP2PManager(SteamLobbyManager lobbyManager, NetworkRules rules)
        {
            _lobbyManager = lobbyManager ?? throw new ArgumentNullException(nameof(lobbyManager));
            _rules = rules ?? new NetworkRules();
            InitializeP2P();
        }

        /// <summary>
        /// Sends a message to a specific player via P2P communication.
        /// </summary>
        /// <param name="targetId">The Steam ID of the target player.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="channel">The communication channel to use (default: 0).</param>
        /// <param name="sendType">The send type for reliability and ordering (default: reliable).</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the message was sent successfully.</returns>
        /// <exception cref="P2PException">Thrown when the P2P manager is not active, target ID is invalid, or sending fails.</exception>
        public async Task<bool> SendMessageAsync(CSteamID targetId, P2PMessage message, int channel = 0, EP2PSend sendType = EP2PSend.k_EP2PSendReliable)
        {
            if (!IsActive)
            {
                throw new P2PException("P2P manager is not active");
            }

            if (!SteamNetworkUtils.IsValidSteamID(targetId))
            {
                throw new P2PException("Invalid target Steam ID", targetId);
            }

            try
            {
                message.SenderId = _lobbyManager.LocalPlayerID;

                var messageData = MessageSerializer.SerializeMessage(message);

                // Apply message policy if provided
                if (_rules.MessagePolicy != null)
                {
                    try
                    {
                        var policy = _rules.MessagePolicy(message);
                        channel = policy.channel;
                        sendType = policy.sendType;
                    }
                    catch { }
                }

                return await SendPacketAsync(targetId, messageData, channel, sendType);
            }
            catch (Exception ex)
            {
                throw new P2PException($"Failed to send message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sends raw packet data to a specific player via P2P communication.
        /// </summary>
        /// <param name="targetId">The Steam ID of the target player.</param>
        /// <param name="data">The raw data to send.</param>
        /// <param name="channel">The communication channel to use (default: 0).</param>
        /// <param name="sendType">The send type for reliability and ordering (default: reliable).</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the packet was sent successfully.</returns>
        /// <exception cref="P2PException">Thrown when the P2P manager is not active, target ID is invalid, packet is too large, or sending fails.</exception>
        public async Task<bool> SendPacketAsync(CSteamID targetId, byte[] data, int channel = 0, EP2PSend sendType = EP2PSend.k_EP2PSendReliable)
        {
            if (!IsActive)
            {
                throw new P2PException("P2P manager is not active");
            }

            if (!SteamNetworkUtils.IsValidSteamID(targetId))
            {
                throw new P2PException("Invalid target Steam ID", targetId);
            }

            if (data.Length > MaxPacketSize)
            {
                throw new P2PException($"Packet too large: {data.Length} bytes (max: {MaxPacketSize})", targetId);
            }

            try
            {
                await EnsureSessionAsync(targetId);

#if IL2CPP
                // IL2CPP-specific: Create a copy of the data and pin it to prevent garbage collection
                // This is critical to prevent memory corruption during P2P transmission
                byte[] dataCopy = new byte[data.Length];
                Array.Copy(data, dataCopy, data.Length);
                
                // Use GC handle to pin the memory so it doesn't get moved or collected
                System.Runtime.InteropServices.GCHandle gcHandle = System.Runtime.InteropServices.GCHandle.Alloc(dataCopy, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    bool success = SteamNetworking.SendP2PPacket(targetId, dataCopy, (uint)dataCopy.Length, sendType, channel);
                    
                    if (success)
                    {
                        _activeSessions[targetId] = DateTime.UtcNow;
                    }
                    
                    OnPacketSent?.Invoke(this, new P2PPacketSentEventArgs(targetId, success, dataCopy.Length, channel, sendType));
                    
                    return success;
                }
                finally
                {
                    // Make sure we always free the GC handle
                    gcHandle.Free();
                }
#else
                bool success = SteamNetworking.SendP2PPacket(targetId, data, (uint)data.Length, sendType, channel);

                if (success)
                {
                    _activeSessions[targetId] = DateTime.UtcNow;
                }

                OnPacketSent?.Invoke(this, new P2PPacketSentEventArgs(targetId, success, data.Length, channel, sendType));

                return success;
#endif
            }
            catch (Exception ex)
            {
                throw new P2PException($"Failed to send packet: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Broadcasts a message to all players in the current lobby.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        /// <param name="channel">The communication channel to use (default: 0).</param>
        /// <param name="sendType">The send type for reliability and ordering (default: reliable).</param>
        /// <exception cref="P2PException">Thrown when not in a lobby.</exception>
        public void BroadcastMessage(P2PMessage message, int channel = 0, EP2PSend sendType = EP2PSend.k_EP2PSendReliable)
        {
            if (!_lobbyManager.IsInLobby)
            {
                throw new P2PException("Cannot broadcast - not in a lobby");
            }

            if (message is StreamMessage streamMessage && sendType == EP2PSend.k_EP2PSendReliable)
            {
                sendType = streamMessage.RecommendedSendType;
            }

#if IL2CPP
            // For IL2CPP, serialize the message once to get the data
            // Set sender ID before serializing
            message.SenderId = _lobbyManager.LocalPlayerID;
            
            // Serialize once to get the data
            byte[] messageData = MessageSerializer.SerializeMessage(message);
            
            // Then broadcast the serialized data using our safe BroadcastPacket method
            BroadcastPacket(messageData, channel, sendType);
#else
            var members = _lobbyManager.GetLobbyMembers();
            foreach (var member in members)
            {
                if (member.SteamId != _lobbyManager.LocalPlayerID)
                {
                    _ = SendMessageAsync(member.SteamId, message, channel, sendType);
                }
            }
#endif
        }

        /// <summary>
        /// Broadcasts raw packet data to all players in the current lobby.
        /// </summary>
        /// <param name="data">The raw data to broadcast.</param>
        /// <param name="channel">The communication channel to use (default: 0).</param>
        /// <param name="sendType">The send type for reliability and ordering (default: reliable).</param>
        /// <exception cref="P2PException">Thrown when not in a lobby.</exception>
        public void BroadcastPacket(byte[] data, int channel = 0, EP2PSend sendType = EP2PSend.k_EP2PSendReliable)
        {
            if (!_lobbyManager.IsInLobby)
            {
                throw new P2PException("Cannot broadcast - not in a lobby");
            }

#if IL2CPP
            // For IL2CPP, we need to make a copy of the data for each send to prevent corruption
            var members = _lobbyManager.GetLobbyMembers();
            foreach (var member in members)
            {
                if (member.SteamId != _lobbyManager.LocalPlayerID)
                {
                    // Create a fresh copy for each member to ensure memory safety
                    byte[] dataCopy = new byte[data.Length];
                    Array.Copy(data, dataCopy, data.Length);
                    
                    // Use the async method but don't await it
                    _ = SendPacketAsync(member.SteamId, dataCopy, channel, sendType);
                }
            }
#else
            var members = _lobbyManager.GetLobbyMembers();
            foreach (var member in members)
            {
                if (member.SteamId != _lobbyManager.LocalPlayerID)
                {
                    _ = SendPacketAsync(member.SteamId, data, channel, sendType);
                }
            }
#endif
        }

        /// <summary>
        /// Registers a handler for a specific message type.
        /// </summary>
        /// <typeparam name="T">The type of message to handle.</typeparam>
        /// <param name="handler">The handler function that will be called when messages of this type are received.</param>
        public void RegisterMessageHandler<T>(Action<T, CSteamID> handler) where T : P2PMessage, new()
        {
            var messageType = new T().MessageType;

            if (!_messageHandlers.ContainsKey(messageType))
            {
                _messageHandlers[messageType] = new List<Action<P2PMessage, CSteamID>>();
            }

#if IL2CPP
            // IL2CPP-specific handler registration to avoid generic type issues
            _messageHandlers[messageType].Add(new System.Action<P2PMessage, CSteamID>((message, senderId) =>
            {
                // Use explicit type check and cast to avoid IL2CPP generic issues
                if (message != null && message.GetType() == typeof(T))
                {
                    try
                    {
                        var typedMessage = (T)message;
                        handler(typedMessage, senderId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error calling handler for exact type: {ex.Message}");
                    }
                }
                else if (message != null && typeof(T).IsAssignableFrom(message.GetType()))
                {
                    try
                    {
                        var typedMessage = (T)message;
                        handler(typedMessage, senderId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error calling handler for assignable type: {ex.Message}");
                    }
                }
            }));
#else
            _messageHandlers[messageType].Add((message, senderId) =>
            {
                if (message is T typedMessage)
                {
                    handler(typedMessage, senderId);
                }
            });
#endif
        }

        /// <summary>
        /// Unregisters all handlers for a specific message type.
        /// </summary>
        /// <typeparam name="T">The type of message to unregister handlers for.</typeparam>
        public void UnregisterMessageHandler<T>() where T : P2PMessage, new()
        {
            var messageType = new T().MessageType;
            _messageHandlers.Remove(messageType);
        }

        /// <summary>
        /// Processes all incoming P2P packets. Call this regularly (e.g., in Update()) to handle received data.
        /// </summary>
        public void ProcessIncomingPackets()
        {
            if (!IsActive) return;

            try
            {
                uint packetSize;
                CSteamID remoteId;

#if IL2CPP
                // IL2CPP-specific: Check multiple channels explicitly & use Il2CppStructArray to avoid corruption
                // The default IsP2PPacketAvailable() without channel parameter may not work in IL2CPP
                int minCh = Math.Max(0, _rules.MinReceiveChannel);
                int maxCh = Math.Max(minCh, _rules.MaxReceiveChannel);
                for (int channel = minCh; channel <= maxCh; channel++)
                {
                    while (SteamNetworking.IsP2PPacketAvailable(out packetSize, channel))
                    {
                        if (packetSize > MaxPacketSize)
                        {
                            // Need to read and discard the oversized packet
                            var discardData = new byte[packetSize];
                            uint discardBytesRead;
                            SteamNetworking.ReadP2PPacket(discardData, packetSize, out discardBytesRead, out remoteId, channel);
                            continue;
                        }

                        // Use Il2CppStructArray directly to avoid marshalling issues
                        var il2cppBuffer = new Il2CppStructArray<byte>(packetSize);
                        uint bytesRead;
                        
                        if (SteamNetworking.ReadP2PPacket(il2cppBuffer, packetSize, out bytesRead, out remoteId, channel))
                        {
                            // Convert Il2CppStructArray to regular byte array
                            byte[] data = new byte[bytesRead];
                            for (int i = 0; i < bytesRead; i++)
                            {
                                data[i] = il2cppBuffer[i];
                            }

                            // Process the packet
                            ProcessReceivedPacket(remoteId, data);
                        }
                    }
                }
#else
                while (SteamNetworking.IsP2PPacketAvailable(out packetSize))
                {
                    if (packetSize > MaxPacketSize)
                    {
                        continue;
                    }

                    var data = new byte[packetSize];
                    uint bytesRead;

                    if (SteamNetworking.ReadP2PPacket(data, packetSize, out bytesRead, out remoteId))
                    {
                        if (bytesRead < packetSize)
                        {
                            var trimmedData = new byte[bytesRead];
                            Array.Copy(data, trimmedData, bytesRead);
                            data = trimmedData;
                        }

                        ProcessReceivedPacket(remoteId, data);
                    }
                }
#endif
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing incoming P2P packets: {ex.Message}");
            }
        }

        /// <summary>
        /// Accepts a P2P session request from another player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player to accept a session with.</param>
        /// <returns>True if the session was accepted successfully, false otherwise.</returns>
        public bool AcceptSession(CSteamID playerId)
        {
            if (!SteamNetworkUtils.IsValidSteamID(playerId))
            {
                return false;
            }

            bool success = SteamNetworking.AcceptP2PSessionWithUser(playerId);
            if (success)
            {
                _activeSessions[playerId] = DateTime.UtcNow;
            }

            return success;
        }

        /// <summary>
        /// Closes a P2P session with a specific player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player to close the session with.</param>
        public void CloseSession(CSteamID playerId)
        {
            if (!SteamNetworkUtils.IsValidSteamID(playerId))
            {
                return;
            }

            SteamNetworking.CloseP2PSessionWithUser(playerId);
            _activeSessions.Remove(playerId);
        }

        /// <summary>
        /// Gets a list of all currently active P2P sessions.
        /// </summary>
        /// <returns>A list of Steam IDs representing active P2P sessions.</returns>
        public List<CSteamID> GetActiveSessions()
        {
            return new List<CSteamID>(_activeSessions.Keys);
        }

        /// <summary>
        /// Gets the current state of a P2P session with a specific player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player to get session state for.</param>
        /// <returns>The current P2P session state information.</returns>
        public P2PSessionState_t GetSessionState(CSteamID playerId)
        {
            P2PSessionState_t sessionState;
            SteamNetworking.GetP2PSessionState(playerId, out sessionState);
            return sessionState;
        }

        /// <summary>
        /// Closes P2P sessions that have been inactive for longer than the specified threshold.
        /// </summary>
        /// <param name="inactiveThreshold">The time threshold for considering a session inactive.</param>
        public void CleanupInactiveSessions(TimeSpan inactiveThreshold)
        {
            var cutoff = DateTime.UtcNow - inactiveThreshold;
            var inactiveSessions = _activeSessions
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in inactiveSessions)
            {
                CloseSession(sessionId);
            }
        }

        private void InitializeP2P()
        {
            if (!SteamNetworkUtils.IsSteamInitialized())
            {
                throw new SteamNetworkException("Steam is not initialized. Make sure Steam is running and SteamAPI.Init() was called.");
            }

#if IL2CPP
            _sessionRequestCallback = Callback<P2PSessionRequest_t>.Create(new System.Action<P2PSessionRequest_t>(OnSessionRequestCallback));
            _sessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(new System.Action<P2PSessionConnectFail_t>(OnSessionConnectFailCallback));
#else
            _sessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnSessionRequestCallback);
            _sessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnSessionConnectFailCallback);
#endif
            // Apply relay rule
            try { SteamNetworking.AllowP2PPacketRelay(_rules.EnableRelay); } catch { }
            IsActive = true;
        }

        private async Task EnsureSessionAsync(CSteamID targetId)
        {
            if (_activeSessions.ContainsKey(targetId))
            {
                return;
            }

            bool accepted = AcceptSession(targetId);
            if (!accepted)
            {
                throw new P2PException($"Failed to establish P2P session with {targetId}", targetId);
            }

            await Task.Delay(50);
        }

        private void ProcessReceivedPacket(CSteamID senderId, byte[] data)
        {
            try
            {
                _activeSessions[senderId] = DateTime.UtcNow;

#if IL2CPP
                // Use explicit event invocation for IL2CPP reliability
                try
                {
                    OnPacketReceived?.Invoke(this, new P2PPacketReceivedEventArgs(senderId, data, 0, (uint)data.Length));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error invoking OnPacketReceived event: {ex.Message}");
                }
#else
                OnPacketReceived?.Invoke(this, new P2PPacketReceivedEventArgs(senderId, data, 0, (uint)data.Length));
#endif

                if (MessageSerializer.IsValidMessage(data))
                {
                    ProcessSteamNetworkLibMessage(senderId, data);
                }
                else
                {
#if IL2CPP
                    // Try to diagnose the issue
                    if (data.Length >= 4)
                    {
                        var possibleHeader = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 4));
                        
                        // Check if it might be JSON directly without our header
                        if (data[0] == '{')
                        {
                            try
                            {
                                var jsonStr = Encoding.UTF8.GetString(data);
                            }
                            catch
                            {
                                // Ignore conversion errors
                            }
                        }
                    }
                    
                    // Throw exception to report the error properly
                    throw new P2PException($"Invalid message format received from {senderId}. Expected header '{MessageSerializer.MESSAGE_HEADER}' not found.");
#endif
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing received packet from {senderId}: {ex.Message}");
            }
        }

        private void ProcessSteamNetworkLibMessage(CSteamID senderId, byte[] data)
        {
            try
            {
                var messageType = MessageSerializer.GetMessageType(data);
                
                if (string.IsNullOrEmpty(messageType)) 
                {
                    return;
                }

                P2PMessage? message = null;

#if IL2CPP
                switch (messageType)
                {
                    // Use explicit if-else instead of switch expression for IL2CPP compatibility
                    case "TEXT":
                        message = MessageSerializer.CreateMessage<TextMessage>(data);
                        break;
                    case "DATA_SYNC":
                        message = MessageSerializer.CreateMessage<DataSyncMessage>(data);
                        break;
                    case "FILE_TRANSFER":
                        message = MessageSerializer.CreateMessage<FileTransferMessage>(data);
                        break;
                    case "STREAM":
                        try
                        {
                            message = MessageSerializer.CreateMessage<StreamMessage>(data);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error creating StreamMessage: {ex.Message}");
                        }

                        break;
                    case "HEARTBEAT":
                        message = MessageSerializer.CreateMessage<HeartbeatMessage>(data);
                        break;
                    case "EVENT":
                        message = MessageSerializer.CreateMessage<EventMessage>(data);
                        break;
                }
#else
                // Original switch expression for Mono
                message = messageType switch
                {
                    "TEXT" => MessageSerializer.CreateMessage<TextMessage>(data),
                    "DATA_SYNC" => MessageSerializer.CreateMessage<DataSyncMessage>(data),
                    "FILE_TRANSFER" => MessageSerializer.CreateMessage<FileTransferMessage>(data),
                    "STREAM" => MessageSerializer.CreateMessage<StreamMessage>(data),
                    "HEARTBEAT" => MessageSerializer.CreateMessage<HeartbeatMessage>(data),
                    "EVENT" => MessageSerializer.CreateMessage<EventMessage>(data),
                    _ => null
                };
#endif

                if (message != null)
                {
#if IL2CPP
                    // Use explicit event invocation for IL2CPP reliability
                    try
                    {
                        if (OnMessageReceived != null)
                        {
                            OnMessageReceived.Invoke(this, new P2PMessageReceivedEventArgs(message, senderId, 0));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error invoking OnMessageReceived event: {ex.Message}");
                    }
#else
                    OnMessageReceived?.Invoke(this, new P2PMessageReceivedEventArgs(message, senderId, 0));
#endif

                    if (_messageHandlers.TryGetValue(messageType, out var handlers))
                    {
                        foreach (var handler in handlers)
                        {
                            try
                            {
                                handler(message, senderId);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error in message handler for {messageType}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing SteamNetworkLib message: {ex.Message}");
            }
        }

        private void OnSessionRequestCallback(P2PSessionRequest_t result)
        {
            try
            {
                var requesterId = result.m_steamIDRemote;
                var requesterName = SteamNetworkUtils.GetPlayerName(requesterId);

                var eventArgs = new P2PSessionRequestEventArgs(requesterId, requesterName);
                if (_rules.AcceptOnlyFriends && !SteamNetworkUtils.IsFriend(requesterId))
                {
                    eventArgs.ShouldAccept = false;
                }
                OnSessionRequested?.Invoke(this, eventArgs);

                if (eventArgs.ShouldAccept)
                {
                    bool success = AcceptSession(requesterId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in session request callback: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates network rules at runtime and applies global settings where possible.
        /// </summary>
        public void UpdateRules(NetworkRules rules)
        {
            if (rules == null) return;
            _rules = rules;
            try { SteamNetworking.AllowP2PPacketRelay(_rules.EnableRelay); } catch { }
        }

        private void OnSessionConnectFailCallback(P2PSessionConnectFail_t result)
        {
            try
            {
                var targetId = result.m_steamIDRemote;
                var error = (EP2PSessionError)result.m_eP2PSessionError;

                _activeSessions.Remove(targetId);

                OnSessionConnectFail?.Invoke(this, new P2PSessionConnectFailEventArgs(targetId, error));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in session connect fail callback: {ex.Message}");
            }
        }

        /// <summary>
        /// Releases all resources used by the SteamP2PManager.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                IsActive = false;

                foreach (var sessionId in _activeSessions.Keys.ToList())
                {
                    CloseSession(sessionId);
                }

                _sessionRequestCallback?.Dispose();
                _sessionConnectFailCallback?.Dispose();

                _messageHandlers.Clear();
                _activeSessions.Clear();
                _sendQueue.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing SteamP2PManager: {ex.Message}");
            }

            _disposed = true;
        }

#if IL2CPP
        /// <summary>
        /// Special debug method to test P2P communication in IL2CPP with a simple raw packet
        /// </summary>
        /// <param name="targetId">The Steam ID of the target player</param>
        /// <returns>True if the test packet was sent successfully</returns>
        public bool SendTestPacket(CSteamID targetId)
        {
            try
            {
                // Create a test message using the proper SNLM format
                var testMessage = new TextMessage
                {
                    Content = $"IL2CPP Test Packet - {DateTime.UtcNow:HH:mm:ss}"
                };
                
                // Serialize using the proper message format
                byte[] testPacket = MessageSerializer.SerializeMessage(testMessage);
                
                // Pin the memory
                System.Runtime.InteropServices.GCHandle gcHandle = 
                    System.Runtime.InteropServices.GCHandle.Alloc(testPacket, System.Runtime.InteropServices.GCHandleType.Pinned);
                
                try
                {
                    // Send directly using Steam API
                    bool success = SteamNetworking.SendP2PPacket(
                        targetId, 
                        testPacket, 
                        (uint)testPacket.Length, 
                        EP2PSend.k_EP2PSendReliable, 
                        0);
                    
                    return success;
                }
                finally
                {
                    gcHandle.Free();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendTestPacket ERROR: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Special debug method to test P2P communication by sending a test packet to all lobby members
        /// </summary>
        /// <returns>True if test packets were sent successfully to at least one member</returns>
        public bool BroadcastTestPacket()
        {
            if (!_lobbyManager.IsInLobby)
            {
                return false;
            }
            
            var members = _lobbyManager.GetLobbyMembers();
            bool anySent = false;
            
            foreach (var member in members)
            {
                if (member.SteamId != _lobbyManager.LocalPlayerID)
                {
                    bool success = SendTestPacket(member.SteamId);
                    if (success)
                    {
                        anySent = true;
                    }
                }
            }
            
            return anySent;
        }

        /// <summary>
        /// Sends an acknowledgment for a test packet
        /// </summary>
        private void SendTestAcknowledgment(CSteamID senderId)
        {
            try
            {
                // Create a test acknowledgment message using proper SNLM format
                var ackMessage = new TextMessage
                {
                    Content = $"Test ACK - {DateTime.UtcNow:HH:mm:ss}"
                };
                
                // Serialize using the proper message format
                byte[] ackPacket = MessageSerializer.SerializeMessage(ackMessage);
                
                // Pin the memory
                System.Runtime.InteropServices.GCHandle gcHandle = 
                    System.Runtime.InteropServices.GCHandle.Alloc(ackPacket, System.Runtime.InteropServices.GCHandleType.Pinned);
                
                try
                {
                    // Send directly using Steam API
                    bool success = SteamNetworking.SendP2PPacket(
                        senderId, 
                        ackPacket, 
                        (uint)ackPacket.Length, 
                        EP2PSend.k_EP2PSendReliable, 
                        0);
                }
                finally
                {
                    gcHandle.Free();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SendTestAcknowledgment ERROR: {ex.Message}");
            }
        }
#endif
    }
}
