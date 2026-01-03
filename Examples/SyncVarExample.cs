using MelonLoader;
using SteamNetworkLib;
using SteamNetworkLib.Sync;
using System;
using System.Collections.Generic;
using System.Linq;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif

// Example assembly attributes (uncomment when using):
// [assembly: MelonInfo(typeof(SyncVarExampleMod.SyncVarExampleMod), "SyncVar-Example", "1.0.0", "SteamNetworkLib")]
// [assembly: MelonGame(null, null)] // Universal mod

namespace SyncVarExampleMod
{
    /// <summary>
    /// Example demonstrating SteamNetworkLib's synchronized variable system.
    /// Shows both HostSyncVar (host-authoritative) and ClientSyncVar (client-owned) usage.
    /// </summary>
    /// <remarks>
    /// This example demonstrates:
    /// - Host-authoritative game state (round number, game settings, timer)
    /// - Client-owned state (ready status, team selection, player loadout)
    /// - Custom serializable types with SyncVars
    /// - Change event handling
    /// - Checking if all players are ready before starting
    /// </remarks>
    public class SyncVarExampleMod : MelonMod
    {
        private SteamNetworkClient? _client;

        // Host-authoritative: Only the host can modify these values
        private HostSyncVar<int>? _roundNumber;
        private HostSyncVar<GameSettings>? _gameSettings;
        private HostSyncVar<GameState>? _gameState;
        private HostSyncVar<float>? _matchTimer;

        // Client-owned: Each client owns their own value
        private ClientSyncVar<bool>? _isReady;
        private ClientSyncVar<string>? _playerTeam;
        private ClientSyncVar<PlayerLoadout>? _playerLoadout;

        public override void OnLateInitializeMelon()
        {
            MelonLogger.Msg("=== SyncVar Example Mod Initializing ===");

            // Initialize SteamNetworkLib
            _client = new SteamNetworkClient();
            if (!_client.Initialize())
            {
                MelonLogger.Error("Failed to initialize SteamNetworkLib!");
                return;
            }

            MelonLogger.Msg("SteamNetworkLib initialized successfully");

            // Subscribe to lobby events to initialize sync vars when we join/create a lobby
            _client.OnLobbyCreated += OnLobbyCreatedOrJoined;
            _client.OnLobbyJoined += OnLobbyCreatedOrJoined;
            _client.OnLobbyLeft += OnLobbyLeft;

            MelonLogger.Msg("=== SyncVar Example Mod Ready ===");
            MelonLogger.Msg("Commands:");
            MelonLogger.Msg("  - Create lobby to test host-authoritative sync");
            MelonLogger.Msg("  - Join lobby to test as client");
        }

        private void OnLobbyCreatedOrJoined(object? sender, EventArgs e)
        {
            MelonLogger.Msg($"Initializing SyncVars (IsHost: {_client?.IsHost})");

            if (_client == null) return;

            // Configure options with a prefix to avoid collisions with other mods
            var options = new NetworkSyncOptions
            {
                KeyPrefix = "SyncVarExample_",
                WarnOnIgnoredWrites = true // Enable warnings for debugging
            };

            // Create host-authoritative sync vars
            _roundNumber = _client.CreateHostSyncVar("RoundNumber", 0, options);
            _gameSettings = _client.CreateHostSyncVar("GameSettings", new GameSettings(), options);
            _gameState = _client.CreateHostSyncVar("GameState", GameState.Lobby, options);
            _matchTimer = _client.CreateHostSyncVar("MatchTimer", 0f, options);

            // Create client-owned sync vars
            _isReady = _client.CreateClientSyncVar("IsReady", false, options);
            _playerTeam = _client.CreateClientSyncVar("PlayerTeam", "spectator", options);
            _playerLoadout = _client.CreateClientSyncVar("PlayerLoadout", new PlayerLoadout(), options);

            // Subscribe to host-authoritative changes
            _roundNumber.OnValueChanged += (oldVal, newVal) =>
            {
                MelonLogger.Msg($"[SYNC] Round number changed: {oldVal} → {newVal}");
            };

            _gameState.OnValueChanged += (oldVal, newVal) =>
            {
                MelonLogger.Msg($"[SYNC] Game state changed: {oldVal} → {newVal}");
                OnGameStateChanged(newVal);
            };

            _gameSettings.OnValueChanged += (oldVal, newVal) =>
            {
                MelonLogger.Msg($"[SYNC] Game settings changed:");
                MelonLogger.Msg($"  Max Players: {newVal.MaxPlayers}");
                MelonLogger.Msg($"  Max Rounds: {newVal.MaxRounds}");
                MelonLogger.Msg($"  Round Time: {newVal.RoundTimeSeconds}s");
            };

            _matchTimer.OnValueChanged += (oldVal, newVal) =>
            {
                // Only log significant changes to avoid spam
                if (Math.Abs(newVal - oldVal) >= 10f)
                {
                    MelonLogger.Msg($"[SYNC] Match timer: {newVal:F1}s");
                }
            };

            // Subscribe to client-owned changes
            _isReady.OnValueChanged += (playerId, oldVal, newVal) =>
            {
                var playerName = GetPlayerName(playerId);
                MelonLogger.Msg($"[SYNC] {playerName} ready status: {oldVal} → {newVal}");
                CheckAllPlayersReady();
            };

            _playerTeam.OnValueChanged += (playerId, oldVal, newVal) =>
            {
                var playerName = GetPlayerName(playerId);
                MelonLogger.Msg($"[SYNC] {playerName} switched team: {oldVal} → {newVal}");
            };

            _playerLoadout.OnValueChanged += (playerId, oldVal, newVal) =>
            {
                var playerName = GetPlayerName(playerId);
                MelonLogger.Msg($"[SYNC] {playerName} changed loadout:");
                MelonLogger.Msg($"  Primary: {newVal.PrimaryWeapon}");
                MelonLogger.Msg($"  Secondary: {newVal.SecondaryWeapon}");
                MelonLogger.Msg($"  Perk: {newVal.Perk}");
            };

            _isReady.OnMyValueChanged += (oldVal, newVal) =>
            {
                MelonLogger.Msg($"[LOCAL] Your ready status: {oldVal} → {newVal}");
            };

            // Subscribe to errors
            _roundNumber.OnSyncError += (ex) =>
            {
                MelonLogger.Error($"[SYNC ERROR] RoundNumber: {ex.Message}");
            };

            _roundNumber.OnWriteIgnored += (attemptedValue) =>
            {
                MelonLogger.Warning($"[SYNC] Non-host tried to set round to {attemptedValue} - ignored");
            };

            MelonLogger.Msg("✓ SyncVars initialized and subscribed");
            
            // Simulate some initial state if we're host
            if (_client.IsHost)
            {
                MelonLogger.Msg("[HOST] Setting initial game state...");
                _gameState.Value = GameState.Lobby;
                _gameSettings.Value = new GameSettings 
                { 
                    MaxPlayers = 4, 
                    MaxRounds = 5,
                    RoundTimeSeconds = 180 
                };
            }
        }

        private void OnLobbyLeft(object? sender, EventArgs e)
        {
            MelonLogger.Msg("Left lobby - SyncVars automatically cleaned up by SteamNetworkLib");

            // Note: No need to manually dispose sync vars - they're automatically
            // disposed when leaving a lobby. Just clear our references.
            _roundNumber = null;
            _gameSettings = null;
            _gameState = null;
            _matchTimer = null;
            _isReady = null;
            _playerTeam = null;
            _playerLoadout = null;
        }

        public override void OnUpdate()
        {
            // Process incoming network messages
            _client?.ProcessIncomingMessages();

            // Host-only: Update match timer if game is in progress
            if (_client?.IsHost == true && _gameState?.Value == GameState.InProgress && _matchTimer != null)
            {
                _matchTimer.Value += UnityEngine.Time.deltaTime;
            }

            // Example hotkeys for testing
            HandleDebugHotkeys();
        }

        private void HandleDebugHotkeys()
        {
            if (_client == null || !_client.IsInLobby) return;

            // F1: Toggle ready status
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F1))
            {
                if (_isReady != null)
                {
                    _isReady.Value = !_isReady.Value;
                    MelonLogger.Msg($"Toggled ready: {_isReady.Value}");
                }
            }

            // F2: Change team (cycles through teams)
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F2))
            {
                if (_playerTeam != null)
                {
                    var teams = new[] { "red", "blue", "spectator" };
                    var currentIndex = Array.IndexOf(teams, _playerTeam.Value);
                    var nextIndex = (currentIndex + 1) % teams.Length;
                    _playerTeam.Value = teams[nextIndex];
                    MelonLogger.Msg($"Changed team to: {_playerTeam.Value}");
                }
            }

            // F3: Change loadout
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F3))
            {
                if (_playerLoadout != null)
                {
                    var loadouts = new[]
                    {
                        new PlayerLoadout { PrimaryWeapon = "Rifle", SecondaryWeapon = "Pistol", Perk = "Speed" },
                        new PlayerLoadout { PrimaryWeapon = "Shotgun", SecondaryWeapon = "Pistol", Perk = "Armor" },
                        new PlayerLoadout { PrimaryWeapon = "Sniper", SecondaryWeapon = "SMG", Perk = "Stealth" }
                    };
                    
                    var random = new Random();
                    _playerLoadout.Value = loadouts[random.Next(loadouts.Length)];
                    MelonLogger.Msg("Changed loadout (check logs for details)");
                }
            }

            // Host-only hotkeys
            if (_client.IsHost)
            {
                // F5: Start game (if all players ready)
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F5))
                {
                    StartGame();
                }

                // F6: End round
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F6))
                {
                    EndRound();
                }

                // F7: Change game settings
                if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F7))
                {
                    if (_gameSettings != null)
                    {
                        var newSettings = _gameSettings.Value;
                        newSettings.MaxRounds++;
                        _gameSettings.Value = newSettings;
                        MelonLogger.Msg($"Increased max rounds to: {newSettings.MaxRounds}");
                    }
                }
            }
        }

        private void StartGame()
        {
            if (!_client?.IsHost == true) return;

            if (!AreAllPlayersReady())
            {
                MelonLogger.Warning("Cannot start game - not all players are ready!");
                return;
            }

            MelonLogger.Msg("[HOST] Starting game...");
            if (_gameState != null)
            {
                _gameState.Value = GameState.InProgress;
            }
            if (_roundNumber != null)
            {
                _roundNumber.Value = 1;
            }
            if (_matchTimer != null)
            {
                _matchTimer.Value = 0f;
            }
        }

        private void EndRound()
        {
            if (!_client?.IsHost == true) return;
            if (_gameState?.Value != GameState.InProgress) return;

            MelonLogger.Msg("[HOST] Ending round...");
            
            if (_roundNumber != null && _gameSettings != null)
            {
                _roundNumber.Value++;
                
                if (_roundNumber.Value > _gameSettings.Value.MaxRounds)
                {
                    MelonLogger.Msg("[HOST] Match complete!");
                    _gameState.Value = GameState.Finished;
                }
                else
                {
                    MelonLogger.Msg($"[HOST] Starting round {_roundNumber.Value}");
                    if (_matchTimer != null)
                    {
                        _matchTimer.Value = 0f;
                    }
                }
            }
        }

        private void CheckAllPlayersReady()
        {
            if (_isReady == null) return;

            var allReady = AreAllPlayersReady();
            var readyCount = _isReady.GetAllValues().Count(kvp => kvp.Value);
            var totalPlayers = _client?.GetLobbyMembers().Count ?? 0;

            MelonLogger.Msg($"[READY CHECK] {readyCount}/{totalPlayers} players ready");

            if (allReady && _client?.IsHost == true)
            {
                MelonLogger.Msg("✓ All players ready! Host can press F5 to start.");
            }
        }

        private bool AreAllPlayersReady()
        {
            if (_isReady == null || _client == null) return false;
            
            var allReadyStates = _isReady.GetAllValues();
            return allReadyStates.Count > 0 && allReadyStates.Values.All(ready => ready);
        }

        private void OnGameStateChanged(GameState newState)
        {
            switch (newState)
            {
                case GameState.Lobby:
                    MelonLogger.Msg("[GAME] In lobby - waiting for players");
                    break;
                case GameState.InProgress:
                    MelonLogger.Msg("[GAME] Match started!");
                    break;
                case GameState.Finished:
                    MelonLogger.Msg("[GAME] Match finished!");
                    break;
            }
        }

        private string GetPlayerName(CSteamID playerId)
        {
            if (_client == null) return "Unknown";
            
            var member = _client.GetLobbyMembers().Find(m => m.SteamId == playerId);
            return member?.DisplayName ?? $"Player {playerId.m_SteamID}";
        }

        public override void OnApplicationQuit()
        {
            MelonLogger.Msg("Shutting down SyncVar example...");
            
            // Note: No need to manually dispose sync vars - they're automatically
            // disposed when the client is disposed
            _client?.Dispose();
            
            MelonLogger.Msg("✓ Cleanup complete");
        }
    }

    #region Custom Serializable Types

    /// <summary>
    /// Example custom type used with HostSyncVar.
    /// Requirements: parameterless constructor, public properties with getters and setters.
    /// </summary>
    public class GameSettings
    {
        public int MaxPlayers { get; set; } = 4;
        public int MaxRounds { get; set; } = 3;
        public int RoundTimeSeconds { get; set; } = 300;
        public bool FriendlyFire { get; set; } = false;
        public string MapName { get; set; } = "default";
    }

    /// <summary>
    /// Example enum used with HostSyncVar.
    /// Enums are automatically serialized as their underlying integer value.
    /// </summary>
    public enum GameState
    {
        Lobby = 0,
        InProgress = 1,
        Finished = 2
    }

    /// <summary>
    /// Example custom type used with ClientSyncVar.
    /// Each client will have their own loadout that others can see.
    /// </summary>
    public class PlayerLoadout
    {
        public string PrimaryWeapon { get; set; } = "Rifle";
        public string SecondaryWeapon { get; set; } = "Pistol";
        public string Perk { get; set; } = "None";
        public List<string> Equipment { get; set; } = new List<string>();
    }

    #endregion
}
