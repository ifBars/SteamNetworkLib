using SteamNetworkLib.Core;
using SteamNetworkLib.Events;
using SteamNetworkLib.Exceptions;
using SteamNetworkLib.Models;
using SteamNetworkLib.Utilities;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MemberInfo = SteamNetworkLib.Models.MemberInfo;
using System.Threading;

namespace SteamNetworkLib
{
    /// <summary>
    /// Main entry point for SteamNetworkLib - provides simplified access to all Steam networking features.
    /// Perfect for use in MelonLoader mods that need Steam lobby and P2P functionality.
    /// </summary>
    public class SteamNetworkClient : IDisposable
    {
        private bool _disposed = false;
        private bool _isInitialized = false;
        private bool _versionCheckEnabled = true;
        private readonly Core.NetworkRules _rules;

        /// <summary>
        /// The internal data key used for storing SteamNetworkLib version information.
        /// </summary>
        private const string STEAMNETWORKLIB_VERSION_KEY = "__snl_version";

        /// <summary>
        /// Gets the current version of SteamNetworkLib.
        /// </summary>
        public static string LibraryVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        /// <summary>
        /// Gets or sets whether automatic version checking is enabled.
        /// When enabled, the library will automatically check for version compatibility between players.
        /// </summary>
        /// <remarks>
        /// <para><strong>IMPORTANT:</strong> Version checking is crucial for ensuring proper data transfer and synchronization between players.</para>
        /// <para>Disabling this feature or ignoring version mismatches may result in:</para>
        /// <list type="bullet">
        /// <item><description>Data serialization/deserialization failures</description></item>
        /// <item><description>Message format incompatibilities</description></item>
        /// <item><description>Synchronization errors</description></item>
        /// <item><description>Unexpected networking behavior or crashes</description></item>
        /// </list>
        /// <para>It is strongly recommended to keep this enabled and ensure all players use the same SteamNetworkLib version.</para>
        /// </remarks>
        public bool VersionCheckEnabled
        {
            get => _versionCheckEnabled;
            set => _versionCheckEnabled = value;
        }

        /// <summary>
        /// Occurs when a version mismatch is detected between players in the lobby.
        /// </summary>
        /// <remarks>
        /// <para><strong>CRITICAL:</strong> This event indicates that players are using incompatible versions of SteamNetworkLib.</para>
        /// <para>Version mismatches can cause serious issues including data corruption, synchronization failures, and networking errors.</para>
        /// </remarks>
        public event EventHandler<VersionMismatchEventArgs>? OnVersionMismatch;

        /// <summary>
        /// Gets the lobby manager for handling Steam lobby operations.
        /// </summary>
        public SteamLobbyManager LobbyManager { get; private set; }

        /// <summary>
        /// Gets the lobby data manager for handling lobby-wide data.
        /// </summary>
        public SteamLobbyData LobbyData { get; private set; }

        /// <summary>
        /// Gets the member data manager for handling player-specific data.
        /// </summary>
        public SteamMemberData MemberData { get; private set; }

        /// <summary>
        /// Gets the P2P manager for handling peer-to-peer communication.
        /// </summary>
        public SteamP2PManager P2PManager { get; private set; }

        /// <summary>
        /// Gets whether the local player is currently in a lobby.
        /// </summary>
        public bool IsInLobby => LobbyManager?.IsInLobby ?? false;

        /// <summary>
        /// Gets whether the local player is the host of the current lobby.
        /// </summary>
        public bool IsHost => LobbyManager?.IsHost ?? false;

        /// <summary>
        /// Gets the Steam ID of the local player.
        /// </summary>
        public CSteamID LocalPlayerId => LobbyManager?.LocalPlayerID ?? CSteamID.Nil;

        /// <summary>
        /// Gets information about the current lobby, or null if not in a lobby.
        /// </summary>
        public LobbyInfo? CurrentLobby => LobbyManager?.CurrentLobby;

        /// <summary>
        /// Occurs when the local player joins a lobby.
        /// </summary>
        public event EventHandler<LobbyJoinedEventArgs>? OnLobbyJoined;

        /// <summary>
        /// Occurs when a new lobby is created.
        /// </summary>
        public event EventHandler<LobbyCreatedEventArgs>? OnLobbyCreated;

        /// <summary>
        /// Occurs when the local player leaves a lobby.
        /// </summary>
        public event EventHandler<LobbyLeftEventArgs>? OnLobbyLeft;

        /// <summary>
        /// Occurs when a new member joins the current lobby.
        /// </summary>
        public event EventHandler<MemberJoinedEventArgs>? OnMemberJoined;

        /// <summary>
        /// Occurs when a member leaves the current lobby.
        /// </summary>
        public event EventHandler<MemberLeftEventArgs>? OnMemberLeft;

        /// <summary>
        /// Occurs when lobby data is changed.
        /// </summary>
        public event EventHandler<LobbyDataChangedEventArgs>? OnLobbyDataChanged;

        /// <summary>
        /// Occurs when member data is changed.
        /// </summary>
        public event EventHandler<MemberDataChangedEventArgs>? OnMemberDataChanged;

        /// <summary>
        /// Occurs when a P2P message is received from another player.
        /// </summary>
        public event EventHandler<P2PMessageReceivedEventArgs>? OnP2PMessageReceived;

        /// <summary>
        /// Current network rules applied to P2P behavior.
        /// </summary>
        public Core.NetworkRules NetworkRules => _rules;

        /// <summary>
        /// Updates network rules at runtime and propagates to managers.
        /// </summary>
        public void UpdateNetworkRules(Core.NetworkRules rules)
        {
            if (rules == null) return;
            if (P2PManager != null)
            {
                P2PManager.UpdateRules(rules);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamNetworkClient"/> class.
        /// Call <see cref="Initialize"/> before using any other methods.
        /// </summary>
        public SteamNetworkClient()
        {
            _rules = new Core.NetworkRules();
        }

        public SteamNetworkClient(Core.NetworkRules rules)
        {
            _rules = rules ?? new Core.NetworkRules();
        }

        /// <summary>
        /// Initializes the Steam networking client. Must be called before using any other methods.
        /// </summary>
        /// <returns>True if initialization was successful.</returns>
        /// <exception cref="SteamNetworkException">Thrown when Steam is not available or when initialization fails.</exception>
        public bool Initialize()
        {
            if (_isInitialized)
            {
                return true;
            }

            try
            {
                if (!SteamNetworkUtils.IsSteamInitialized())
                {
                    throw new SteamNetworkException("Steam is not initialized. Make sure Steam is running and SteamAPI.Init() was called.");
                }

                LobbyManager = new SteamLobbyManager();
                LobbyData = new SteamLobbyData(LobbyManager);
                MemberData = new SteamMemberData(LobbyManager);
                P2PManager = new SteamP2PManager(LobbyManager, _rules);

                SubscribeToEvents();

                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                throw new SteamNetworkException($"Failed to initialize SteamNetworkClient: {ex.Message}", ex);
            }
        }

        #region Lobby Management

        /// <summary>
        /// Creates a new lobby with the specified settings.
        /// </summary>
        /// <param name="lobbyType">The type of lobby to create.</param>
        /// <param name="maxMembers">The maximum number of members allowed in the lobby.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the lobby information.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when lobby creation fails.</exception>
        public async Task<LobbyInfo> CreateLobbyAsync(ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly, int maxMembers = 4)
        {
            EnsureInitialized();
            return await LobbyManager.CreateLobbyAsync(lobbyType, maxMembers);
        }

        /// <summary>
        /// Joins an existing lobby by ID.
        /// </summary>
        /// <param name="lobbyId">The Steam ID of the lobby to join.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the lobby information.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when joining the lobby fails.</exception>
        public async Task<LobbyInfo> JoinLobbyAsync(CSteamID lobbyId)
        {
            EnsureInitialized();
            return await LobbyManager.JoinLobbyAsync(lobbyId);
        }

        /// <summary>
        /// Leaves the current lobby.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <remarks>
        /// This method has no effect if the local player is not currently in a lobby.
        /// </remarks>
        public void LeaveLobby()
        {
            EnsureInitialized();
            LobbyManager.LeaveLobby();
        }

        /// <summary>
        /// Gets all members in the current lobby.
        /// </summary>
        /// <returns>A list of <see cref="MemberInfo"/> objects for all players in the lobby.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <remarks>
        /// Returns an empty list if not currently in a lobby.
        /// </remarks>
        public List<MemberInfo> GetLobbyMembers()
        {
            EnsureInitialized();
            return LobbyManager.GetLobbyMembers();
        }

        /// <summary>
        /// Invites a friend to the current lobby.
        /// </summary>
        /// <param name="friendId">The Steam ID of the friend to invite.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or invitation fails.</exception>
        public void InviteFriend(CSteamID friendId)
        {
            EnsureInitialized();
            LobbyManager.InviteFriend(friendId);
        }

        /// <summary>
        /// Opens the Steam overlay invite dialog.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or the overlay fails to open.</exception>
        public void OpenInviteDialog()
        {
            EnsureInitialized();
            LobbyManager.OpenInviteDialog();
        }

        #endregion

        #region Data Management

        /// <summary>
        /// Sets lobby-wide data that is accessible to all players.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <param name="value">The data value.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or not the lobby owner.</exception>
        /// <remarks>
        /// Only the lobby owner can set lobby data. This method will fail silently if called by a non-owner.
        /// </remarks>
        public void SetLobbyData(string key, string value)
        {
            EnsureInitialized();
            LobbyData.SetData(key, value);
        }

        /// <summary>
        /// Gets lobby-wide data.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <returns>The data value, or null if not found.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public string? GetLobbyData(string key)
        {
            EnsureInitialized();
            return LobbyData.GetData(key);
        }

        /// <summary>
        /// Sets data for the local player that is visible to all players.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <param name="value">The data value.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public void SetMyData(string key, string value)
        {
            EnsureInitialized();
            MemberData.SetMemberData(key, value);
        }

        /// <summary>
        /// Gets data for the local player.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <returns>The data value, or null if not found.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public string? GetMyData(string key)
        {
            EnsureInitialized();
            return MemberData.GetMemberData(key);
        }

        /// <summary>
        /// Gets data for a specific player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player.</param>
        /// <param name="key">The data key.</param>
        /// <returns>The data value, or null if not found.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or the player is not in the lobby.</exception>
        public string? GetPlayerData(CSteamID playerId, string key)
        {
            EnsureInitialized();
            return MemberData.GetMemberData(playerId, key);
        }

        /// <summary>
        /// Gets the same data key for all players in the lobby.
        /// </summary>
        /// <param name="key">The data key.</param>
        /// <returns>A dictionary mapping player Steam IDs to their data values.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public Dictionary<CSteamID, string> GetDataForAllPlayers(string key)
        {
            EnsureInitialized();
            return MemberData.GetMemberDataForAllPlayers(key);
        }

        /// <summary>
        /// Sets multiple data values at once for the local player.
        /// </summary>
        /// <param name="data">A dictionary containing key-value pairs to set.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public void SetMyDataBatch(Dictionary<string, string> data)
        {
            EnsureInitialized();
            MemberData.SetMemberDataBatch(data);
        }

        #endregion

        #region P2P Communication

        /// <summary>
        /// Sends a message to a specific player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the target player.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the message was sent successfully.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        public async Task<bool> SendMessageToPlayerAsync(CSteamID playerId, P2PMessage message)
        {
            EnsureInitialized();
            return await P2PManager.SendMessageAsync(playerId, message);
        }

        /// <summary>
        /// Sends a message to all players in the lobby.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This method sends the message to each player individually.
        /// </remarks>
        public async Task BroadcastMessageAsync(P2PMessage message)
        {
            EnsureInitialized();
            
            var members = LobbyManager.GetLobbyMembers();
            var sendTasks = new List<Task<bool>>();
            
            // Create a task for each send operation
            foreach (var member in members)
            {
                if (member.SteamId != LobbyManager.LocalPlayerID)
                {
                    sendTasks.Add(P2PManager.SendMessageAsync(member.SteamId, message));
                }
            }
            
            // Wait for all send operations to complete
            if (sendTasks.Count > 0)
            {
                await Task.WhenAll(sendTasks);
            }
        }

        /// <summary>
        /// Sends a message to all players in the lobby. This is a non-async wrapper around BroadcastMessageAsync.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This method is provided for backward compatibility. For new code, use BroadcastMessageAsync instead.
        /// </remarks>
        public void BroadcastMessage(P2PMessage message)
        {
            EnsureInitialized();
            
            // Fire and forget the async operation
            // This isn't ideal, but maintains backward compatibility
            _ = BroadcastMessageAsync(message);
        }

        /// <summary>
        /// Sends a simple text message to a player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the target player.</param>
        /// <param name="text">The text message to send.</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the message was sent successfully.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This is a convenience method that creates a <see cref="TextMessage"/> internally.
        /// </remarks>
        public async Task<bool> SendTextMessageAsync(CSteamID playerId, string text)
        {
            EnsureInitialized();
            var message = new TextMessage { Content = text };
            return await P2PManager.SendMessageAsync(playerId, message);
        }

        /// <summary>
        /// Broadcasts a simple text message to all players.
        /// </summary>
        /// <param name="text">The text message to broadcast.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This is a convenience method that creates a <see cref="TextMessage"/> internally.
        /// </remarks>
        public async Task BroadcastTextMessageAsync(string text)
        {
            EnsureInitialized();
            var message = new TextMessage { Content = text };
            await BroadcastMessageAsync(message);
        }

        /// <summary>
        /// Broadcasts a simple text message to all players. This is a non-async wrapper around BroadcastTextMessageAsync.
        /// </summary>
        /// <param name="text">The text message to broadcast.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This method is provided for backward compatibility. For new code, use BroadcastTextMessageAsync instead.
        /// </remarks>
        public void BroadcastTextMessage(string text)
        {
            EnsureInitialized();
            
            // Fire and forget the async operation
            // This isn't ideal, but maintains backward compatibility
            _ = BroadcastTextMessageAsync(text);
        }

        /// <summary>
        /// Sends a data synchronization message to a player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the target player.</param>
        /// <param name="key">The data key.</param>
        /// <param name="value">The data value.</param>
        /// <param name="dataType">The data type identifier.</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the message was sent successfully.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="P2PException">Thrown when the message cannot be sent.</exception>
        /// <remarks>
        /// This is a convenience method that creates a <see cref="DataSyncMessage"/> internally.
        /// </remarks>
        public async Task<bool> SendDataSyncAsync(CSteamID playerId, string key, string value, string dataType = "string")
        {
            EnsureInitialized();
            var message = new DataSyncMessage
            {
                Key = key,
                Value = value,
                DataType = dataType
            };
            return await P2PManager.SendMessageAsync(playerId, message);
        }

        /// <summary>
        /// Registers a handler for a specific message type.
        /// </summary>
        /// <typeparam name="T">The type of message to handle.</typeparam>
        /// <param name="handler">The handler function that will be called when messages of this type are received.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <remarks>
        /// The handler will be called with the message and the sender's Steam ID.
        /// </remarks>
        public void RegisterMessageHandler<T>(Action<T, CSteamID> handler) where T : P2PMessage, new()
        {
            EnsureInitialized();
            P2PManager.RegisterMessageHandler(handler);
        }

        /// <summary>
        /// Processes incoming P2P packets. Call this regularly (e.g., in Update()).
        /// </summary>
        /// <remarks>
        /// This method should be called frequently to ensure timely processing of incoming messages.
        /// If not called regularly, messages may be delayed or dropped.
        /// </remarks>
        public void ProcessIncomingMessages()
        {
            if (!_isInitialized) return;

#if IL2CPP
            // CRITICAL: Must call SteamAPI.RunCallbacks() in IL2CPP to process Steam callbacks
            // This is what actually triggers P2P packet reception callbacks
            try
            {
                SteamAPI.RunCallbacks();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamNetworkLib] Error running Steam callbacks: {ex.Message}");
            }
#endif

            P2PManager.ProcessIncomingPackets();
        }

        #endregion

        #region High-Level Helper Methods

        /// <summary>
        /// Synchronizes data with all players in the lobby.
        /// Useful for mod compatibility checks and data synchronization.
        /// </summary>
        /// <param name="dataKey">The data key to synchronize.</param>
        /// <param name="dataValue">The data value to synchronize.</param>
        /// <param name="dataType">The data type identifier.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        /// <remarks>
        /// This method both sets the local player's data and broadcasts it to all other players.
        /// </remarks>
        public async Task SyncModDataWithAllPlayersAsync(string dataKey, string dataValue, string dataType = "string")
        {
            EnsureInitialized();

            SetMyData(dataKey, dataValue);

            var syncMessage = new DataSyncMessage
            {
                Key = dataKey,
                Value = dataValue,
                DataType = dataType
            };
            
            await BroadcastMessageAsync(syncMessage);
        }

        /// <summary>
        /// Synchronizes data with all players in the lobby. This is a non-async wrapper around SyncModDataWithAllPlayersAsync.
        /// </summary>
        /// <param name="dataKey">The data key to synchronize.</param>
        /// <param name="dataValue">The data value to synchronize.</param>
        /// <param name="dataType">The data type identifier.</param>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        /// <remarks>
        /// This method is provided for backward compatibility. For new code, use SyncModDataWithAllPlayersAsync instead.
        /// </remarks>
        public void SyncModDataWithAllPlayers(string dataKey, string dataValue, string dataType = "string")
        {
            EnsureInitialized();

            SetMyData(dataKey, dataValue);

            var syncMessage = new DataSyncMessage
            {
                Key = dataKey,
                Value = dataValue,
                DataType = dataType
            };
            BroadcastMessage(syncMessage);
        }

        /// <summary>
        /// Checks if all players have compatible mod data for a given key.
        /// </summary>
        /// <param name="dataKey">The data key to check for compatibility.</param>
        /// <returns>True if all players have the same data value, false otherwise.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        /// <remarks>
        /// This method is useful for verifying that all players are using the same mod version.
        /// </remarks>
        public bool IsModDataCompatible(string dataKey)
        {
            EnsureInitialized();

            var allPlayerData = GetDataForAllPlayers(dataKey);
            if (allPlayerData.Count <= 1) return true;

            var values = allPlayerData.Values.Distinct().ToArray();
            return values.Length == 1;
        }

        /// <summary>
        /// Sets the SteamNetworkLib version information for the local player.
        /// This is automatically called when joining or creating lobbies if version checking is enabled.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        internal void SetLibraryVersionData()
        {
            EnsureInitialized();
            SetMyData(STEAMNETWORKLIB_VERSION_KEY, LibraryVersion);
        }

        /// <summary>
        /// Performs a comprehensive version check and fires the OnVersionMismatch event if incompatibilities are found.
        /// </summary>
        /// <returns>True if all versions are compatible, false if mismatches were detected.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public bool CheckLibraryVersionCompatibility()
        {
            EnsureInitialized();

            if (!_versionCheckEnabled)
            {
                return true; // Skip check if disabled
            }

            var playerVersions = GetDataForAllPlayers(STEAMNETWORKLIB_VERSION_KEY);
            
            // If no version data exists yet, consider it compatible
            if (playerVersions.Count == 0)
            {
                return true;
            }

            // Check for version mismatches
            var distinctVersions = playerVersions.Values.Distinct().ToList();
            if (distinctVersions.Count <= 1)
            {
                return true; // All versions are the same
            }

            // Version mismatch detected - find incompatible players
            var localVersion = LibraryVersion;
            var incompatiblePlayers = new List<CSteamID>();

            foreach (var kvp in playerVersions)
            {
                if (kvp.Value != localVersion)
                {
                    incompatiblePlayers.Add(kvp.Key);
                }
            }

            // Fire the version mismatch event
            try
            {
                OnVersionMismatch?.Invoke(this, new VersionMismatchEventArgs(localVersion, playerVersions, incompatiblePlayers));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in version mismatch event handler: {ex.Message}");
            }

            // Log the version mismatch
            var versionsInfo = string.Join(", ", playerVersions.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
            Console.WriteLine($"[SteamNetworkLib] WARNING: Version mismatch detected! Local version: {localVersion}, Player versions: {versionsInfo}");

            return false;
        }

        /// <summary>
        /// Gets the SteamNetworkLib versions of all players in the lobby.
        /// </summary>
        /// <returns>A dictionary mapping player Steam IDs to their SteamNetworkLib versions. Players without version data are excluded.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        /// <remarks>
        /// <para>Use this method to identify which players have different library versions that could cause data transfer issues.</para>
        /// <para>Players with missing version data may be using older versions of SteamNetworkLib that don't support version checking,</para>
        /// <para>which could result in unpredictable networking behavior and synchronization failures.</para>
        /// </remarks>
        public Dictionary<CSteamID, string> GetPlayerLibraryVersions()
        {
            EnsureInitialized();
            return GetDataForAllPlayers(STEAMNETWORKLIB_VERSION_KEY);
        }

        #endregion

        /// <summary>
        /// Subscribes to events from the component managers.
        /// </summary>
        /// <remarks>
        /// This method is called during initialization to set up event forwarding.
        /// </remarks>
        private void SubscribeToEvents()
        {
            // Simple event forwarding
            LobbyManager.OnLobbyJoined += (s, e) => OnLobbyJoined?.Invoke(this, e);
            LobbyManager.OnLobbyCreated += (s, e) => OnLobbyCreated?.Invoke(this, e);
            LobbyManager.OnLobbyLeft += (s, e) => OnLobbyLeft?.Invoke(this, e);
            LobbyManager.OnMemberJoined += (s, e) => OnMemberJoined?.Invoke(this, e);
            LobbyManager.OnMemberLeft += (s, e) => OnMemberLeft?.Invoke(this, e);
            LobbyData.OnLobbyDataChanged += (s, e) => OnLobbyDataChanged?.Invoke(this, e);
            P2PManager.OnMessageReceived += (s, e) => OnP2PMessageReceived?.Invoke(this, e);

            // Add version checking if enabled
            if (!_versionCheckEnabled) return;
            LobbyManager.OnLobbyJoined += (s, e) => SafeExecute(() => SetLibraryVersionData(), "setting version data on lobby join");
            LobbyManager.OnLobbyCreated += (s, e) => SafeExecute(() => SetLibraryVersionData(), "setting version data on lobby creation");
            LobbyManager.OnMemberJoined += (s, e) => SafeExecute(async () => { await Task.Delay(500); CheckLibraryVersionCompatibility(); }, "checking version compatibility");
            MemberData.OnMemberDataChanged += (s, e) => { if (e.Key == STEAMNETWORKLIB_VERSION_KEY) SafeExecute(() => CheckLibraryVersionCompatibility(), "checking version compatibility on data change"); };
        }

        private void SafeExecute(Action action, string operation)
        {
            try { action(); }
            catch (Exception ex) { Console.WriteLine($"[SteamNetworkLib] Warning: Failed {operation}: {ex.Message}"); }
        }

        private void SafeExecute(Func<Task> action, string operation)
        {
            Task.Run(async () => { try { await action(); } catch (Exception ex) { Console.WriteLine($"[SteamNetworkLib] Warning: Failed {operation}: {ex.Message}"); } });
        }

        /// <summary>
        /// Unsubscribes from events from the component managers.
        /// </summary>
        /// <remarks>
        /// This method is called during disposal to clean up event handlers.
        /// </remarks>
        private void UnsubscribeFromEvents()
        {
            // Clear our own event handlers
            OnLobbyJoined = null;
            OnLobbyCreated = null;
            OnLobbyLeft = null;
            OnMemberJoined = null;
            OnMemberLeft = null;
            OnLobbyDataChanged = null;
            OnMemberDataChanged = null;
            OnP2PMessageReceived = null;
            OnVersionMismatch = null;
        }

        /// <summary>
        /// Ensures that the client is initialized before performing operations.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the client is not initialized.</exception>
        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("SteamNetworkClient is not initialized. Call Initialize() first.");
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="SteamNetworkClient"/>.
        /// </summary>
        /// <remarks>
        /// This method disposes all component managers and releases any unmanaged resources.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Unsubscribe from all events first
                UnsubscribeFromEvents();

                // Then dispose components
                P2PManager?.Dispose();
                MemberData?.Dispose();
                LobbyData?.Dispose();
                LobbyManager?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing SteamNetworkClient: {ex.Message}");
            }

            _disposed = true;
            
            // Suppress finalization as there's nothing for the GC to clean up
            GC.SuppressFinalize(this);
        }
    }
}
