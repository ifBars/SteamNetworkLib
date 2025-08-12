#if SCHEDULE_ONE_INTEGRATION
using MelonLoader;
using ScheduleOne.Networking;
using SteamNetworkLib;
using SteamNetworkLib.Models;
using Steamworks;

// [assembly: MelonInfo(typeof(ScheduleOneNetworkingMod.ScheduleOneNetworkingMod), "ScheduleOne-NetworkingExample", "1.0.0", "SteamNetworkLib")]
// [assembly: MelonGame("TVGS", "Schedule I")]

namespace ScheduleOneNetworkingMod
{
    /// <summary>
    /// Minimal example showing how SteamNetworkLib adds powerful networking to ScheduleOne with just a few lines of code.
    /// This leverages ScheduleOne's existing Lobby class and adds P2P messaging, data sync, and mod compatibility checking.
    /// </summary>
    public class ScheduleOneNetworkingMod : MelonMod
    {
        private SteamNetworkClient? _networkClient;
        private readonly string _modVersion = "1.2.3";
        private readonly string _compatibilityHash = "schedule_one_v1.2.3";

        public override void OnLateInitializeMelon()
        {
            MelonLogger.Msg("Adding advanced networking to ScheduleOne...");

            // Initialize SteamNetworkLib
            _networkClient = new SteamNetworkClient();
            if (_networkClient.Initialize())
            {
                // Hook into Steamworks & ScheduleOne network events
                SetupNetworkingEvents();
                MelonLogger.Msg("✓ Advanced networking features added!");
            }
        }

        private void SetupNetworkingEvents()
        {
            if (_networkClient == null) return;

            // Hook into ScheduleOne's lobby events
            Lobby.Instance.onLobbyChange += OnLobbyChanged;

            // Set up P2P message handlers
            _networkClient.RegisterMessageHandler<TextMessage>(OnChatMessage);
            _networkClient.RegisterMessageHandler<DataSyncMessage>(OnDataSync);
        }

        private void OnLobbyChanged()
        {
            if (_networkClient == null || !Lobby.Instance.IsInLobby) return;

            // Automatically sync our mod data when lobby changes
            _networkClient.SetMyData("mod_version", _modVersion);
            _networkClient.SetMyData("compatibility_hash", _compatibilityHash);

            // Broadcast compatibility to all players via P2P
            _networkClient.SyncModDataWithAllPlayers("compatibility_hash", _compatibilityHash);

            // Check if everyone has compatible versions
            CheckModCompatibility();
        }

        private void CheckModCompatibility()
        {
            if (_networkClient == null) return;

            bool isCompatible = _networkClient.IsModDataCompatible("compatibility_hash");

            if (isCompatible)
            {
                MelonLogger.Msg("✓ All players have compatible mod versions!");
                EnableAdvancedFeatures();
            }
            else
            {
                MelonLogger.Warning("⚠ Version mismatch detected - advanced features disabled");
                ShowCompatibilityInfo();
            }
        }

        private void ShowCompatibilityInfo()
        {
            var compatibilityData = _networkClient?.GetDataForAllPlayers("compatibility_hash");
            if (compatibilityData == null) return;

            MelonLogger.Warning("=== MOD COMPATIBILITY ===");
            MelonLogger.Warning($"Your version: {_compatibilityHash}");
            foreach (var kvp in compatibilityData)
            {
                var playerName = _networkClient.GetLobbyMembers()
                    .Find(m => m.SteamId == kvp.Key)?.DisplayName ?? "Unknown";
                MelonLogger.Warning($"{playerName}: {kvp.Value}");
            }
        }

        private void OnChatMessage(TextMessage message, CSteamID senderId)
        {
            var playerName = _networkClient?.GetLobbyMembers()
                .Find(m => m.SteamId == senderId)?.DisplayName ?? "Unknown";
            MelonLogger.Msg($"[P2P CHAT] {playerName}: {message.Content}");
        }

        private void OnDataSync(DataSyncMessage message, CSteamID senderId)
        {
            MelonLogger.Msg($"Data sync: {message.Key} = {message.Value} (from {senderId})");

            // Example: Sync player preferences across all clients
            if (message.Key == "player_preferences")
            {
                HandlePlayerPreferencesSync(message.Value, senderId);
            }
        }

        private void HandlePlayerPreferencesSync(string preferencesJson, CSteamID senderId)
        {
            // Your game-specific logic here
            MelonLogger.Msg($"Synced player preferences from {senderId}");
        }

        private void EnableAdvancedFeatures()
        {
            // Enable features that require all players to have the same mod version
            MelonLogger.Msg("Advanced multiplayer features enabled!");
        }

        public override void OnUpdate()
        {
            // Process P2P messages (essential for real-time communication)
            _networkClient?.ProcessIncomingMessages();
        }

        public override void OnApplicationQuit()
        {
            _networkClient?.Dispose();
        }

        // Public API for other mods to send data
        public void BroadcastToAllPlayers(string message)
        {
            _networkClient?.BroadcastTextMessage($"[MOD] {message}");
        }

        public void SyncGameData(string key, string value)
        {
            _networkClient?.SyncModDataWithAllPlayers(key, value);
        }
    }
}
#endif