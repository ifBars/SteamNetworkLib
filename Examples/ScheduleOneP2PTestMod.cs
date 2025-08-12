using MelonLoader;
using SteamNetworkLib;
using SteamNetworkLib.Models;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[assembly: MelonInfo(typeof(ScheduleOneP2PTestMod.ScheduleOneP2PTestMod), "ScheduleOne-P2PTest", "1.0.0", "Bars")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleOneP2PTestMod
{
    /// <summary>
    /// Basic SteamNetworkLib P2P test mod for Schedule 1.
    /// Tests P2P packet sending and receiving functionality.
    /// </summary>
    public class ScheduleOneP2PTestMod : MelonMod
    {
        private SteamNetworkClient? _networkClient;
        private int _packetCounter = 0;
        private DateTime _lastTestTime = DateTime.MinValue;
        private readonly TimeSpan _testInterval = TimeSpan.FromSeconds(10); // Send test packet every 10 seconds
        private bool _isInitialized = false;

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName != "Menu" || _isInitialized) return;
            MelonLogger.Msg("=== Schedule One P2P Test Mod ===");
            MelonLogger.Msg("Initializing P2P networking test...");

            try
            {
                // Initialize SteamNetworkLib
                _networkClient = new SteamNetworkClient();
                if (_networkClient.Initialize())
                {
                    SteamNetworking.AllowP2PPacketRelay(false);
                    SetupP2PEvents();
                    _isInitialized = true;
                    MelonLogger.Msg("✓ P2P networking initialized successfully!");
                    MelonLogger.Msg("✓ Mod will automatically test P2P every 10 seconds when in a lobby");
                }
                else
                {
                    MelonLogger.Error("✗ Failed to initialize P2P networking");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"✗ Error initializing P2P networking: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        private void SetupP2PEvents()
        {
            if (_networkClient == null) return;

            // Register handlers for different message types
            _networkClient.RegisterMessageHandler<TextMessage>(OnTextMessageReceived);
            _networkClient.RegisterMessageHandler<DataSyncMessage>(OnDataSyncReceived);
            _networkClient.RegisterMessageHandler<HeartbeatMessage>(OnHeartbeatReceived);

            // Subscribe to P2P events
            _networkClient.OnP2PMessageReceived += OnP2PMessageReceived;

            MelonLogger.Msg("✓ P2P event handlers registered");
        }

        private void OnTextMessageReceived(TextMessage message, CSteamID senderId)
        {
            var senderName = GetPlayerName(senderId);
            MelonLogger.Msg($"[P2P TEXT] {senderName}: {message.Content}");
        }

        private void OnDataSyncReceived(DataSyncMessage message, CSteamID senderId)
        {
            var senderName = GetPlayerName(senderId);
            MelonLogger.Msg($"[P2P DATA] {senderName} - {message.Key}: {message.Value}");
        }

        private void OnHeartbeatReceived(HeartbeatMessage message, CSteamID senderId)
        {
            var senderName = GetPlayerName(senderId);
            
            // Calculate latency using high precision timestamp for accurate measurement
            var currentTicks = DateTime.UtcNow.Ticks;
            var latencyTicks = currentTicks - message.HighPrecisionTimestamp;
            var latencyMs = latencyTicks / TimeSpan.TicksPerMillisecond;
            
            MelonLogger.Msg($"[P2P HEARTBEAT] {senderName} - Latency: {latencyMs:F1}ms (IsResponse: {message.IsResponse}, Seq: {message.SequenceNumber})");
        }

        private void OnP2PMessageReceived(object? sender, SteamNetworkLib.Events.P2PMessageReceivedEventArgs e)
        {
            var senderName = GetPlayerName(e.SenderId);
            MelonLogger.Msg($"[P2P EVENT] Received {e.Message.MessageType} message from {senderName}");
        }

        private string GetPlayerName(CSteamID steamId)
        {
            if (_networkClient == null || !_isInitialized) return "Unknown";
            
            try
            {
                var members = _networkClient.GetLobbyMembers();
                var member = members.Find(m => m.SteamId == steamId);
                return member?.DisplayName ?? steamId.ToString();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting player name: {ex.Message}");
                return steamId.ToString();
            }
        }

        public override void OnUpdate()
        {
            // Only process if fully initialized
            if (_networkClient == null || !_isInitialized) return;

            try
            {
                // Process incoming P2P messages
                _networkClient.ProcessIncomingMessages();

                // Check if we're in a lobby before trying to send packets
                if (!IsInLobby() || DateTime.UtcNow - _lastTestTime < _testInterval) return;
                SendTestPackets();
                _lastTestTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in OnUpdate: {ex.Message}");
                if (ex is NullReferenceException)
                {
                    MelonLogger.Error("Network client may not be fully initialized yet");
                }
            }
        }

        private bool IsInLobby()
        {
            try
            {
                return _networkClient?.IsInLobby ?? false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking lobby status: {ex.Message}");
                return false;
            }
        }

        private void SendTestPackets()
        {
            if (_networkClient == null || !_isInitialized) return;

            try
            {
                if (!IsInLobby()) return;

                _packetCounter++;

                // Test 1: Send text message
                var textMessage = new TextMessage
                {
                    Content = $"P2P Test #{_packetCounter} from {_networkClient.LocalPlayerId} at {DateTime.UtcNow:HH:mm:ss}"
                };
                _networkClient.BroadcastMessage(textMessage);
                MelonLogger.Msg($"[SENT] Text message #{_packetCounter}");

                // Test 2: Send data sync message
                var dataMessage = new DataSyncMessage
                {
                    Key = "test_data",
                    Value = $"packet_{_packetCounter}_{DateTime.UtcNow.Ticks}",
                    DataType = "test"
                };
                _networkClient.BroadcastMessage(dataMessage);
                MelonLogger.Msg($"[SENT] Data sync message #{_packetCounter}");

                // Test 3: Send heartbeat message
                var heartbeatMessage = new HeartbeatMessage();
                _networkClient.BroadcastMessage(heartbeatMessage);
                MelonLogger.Msg($"[SENT] Heartbeat message #{_packetCounter}");

                MelonLogger.Msg($"=== Sent {_packetCounter} test packets to all lobby members ===");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"✗ Error sending test packets: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        public override void OnApplicationQuit()
        {
            try
            {
                _networkClient?.Dispose();
                _isInitialized = false;
                MelonLogger.Msg("✓ P2P test mod disposed");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"✗ Error disposing P2P test mod: {ex.Message}");
            }
        }
    }
} 