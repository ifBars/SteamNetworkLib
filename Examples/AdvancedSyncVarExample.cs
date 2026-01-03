using System;
using SteamNetworkLib;
using SteamNetworkLib.Sync;

namespace SteamNetworkLib.Examples
{
    /// <summary>
    /// Advanced SyncVar example demonstrating validation, rate limiting, and batch syncing.
    /// </summary>
    /// <remarks>
    /// This example showcases:
    /// <list type="bullet">
    /// <item><description>Value validation using built-in validators</description></item>
    /// <item><description>Rate limiting for high-frequency updates</description></item>
    /// <item><description>Manual batching with AutoSync disabled</description></item>
    /// <item><description>Custom validators for complex validation logic</description></item>
    /// </list>
    /// 
    /// Hotkeys:
    /// <list type="bullet">
    /// <item><description>F1: Demonstrate range validation (valid score)</description></item>
    /// <item><description>F2: Demonstrate range validation (invalid score - too high)</description></item>
    /// <item><description>F3: Demonstrate rate limiting (rapid position updates)</description></item>
    /// <item><description>F4: Demonstrate batch sync (multiple changes, single sync)</description></item>
    /// <item><description>F5: Demonstrate custom validator (team name validation)</description></item>
    /// <item><description>F6: Check if any sync vars are dirty</description></item>
    /// <item><description>F7: Flush all pending changes</description></item>
    /// </list>
    /// </remarks>
    public class AdvancedSyncVarExample
    {
        private SteamNetworkClient _client;
        
        // Example 1: Range validation
        private HostSyncVar<int>? _playerScore;
        
        // Example 2: Rate limiting for position updates
        private ClientSyncVar<float>? _playerPositionX;
        
        // Example 3: Batch syncing with AutoSync disabled
        private HostSyncVar<string>? _gamePhase;
        private HostSyncVar<int>? _roundNumber;
        
        // Example 4: Custom validation
        private ClientSyncVar<string>? _teamName;

        public AdvancedSyncVarExample(SteamNetworkClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            InitializeSyncVars();
        }

        private void InitializeSyncVars()
        {
            // Example 1: Score with range validation (0-9999)
            // Invalid scores will be rejected and logged
            var scoreValidator = new RangeValidator<int>(0, 9999);
            _playerScore = _client.CreateHostSyncVar(
                "PlayerScore", 
                0, 
                new NetworkSyncOptions { WarnOnIgnoredWrites = true },
                scoreValidator
            );

            _playerScore.OnValueChanged += (oldVal, newVal) =>
            {
                Console.WriteLine($"[AdvancedExample] Score changed: {oldVal} -> {newVal}");
            };

            _playerScore.OnSyncError += (ex) =>
            {
                Console.WriteLine($"[AdvancedExample] Score validation failed: {ex.Message}");
            };

            // Example 2: Position with rate limiting (max 10 updates/second)
            // Useful for high-frequency data like player positions
            var positionOptions = new NetworkSyncOptions
            {
                MaxSyncsPerSecond = 10,
                KeyPrefix = "Advanced_"
            };

            _playerPositionX = _client.CreateClientSyncVar(
                "PositionX",
                0f,
                positionOptions
            );

            _playerPositionX.OnMyValueChanged += (oldVal, newVal) =>
            {
                Console.WriteLine($"[AdvancedExample] My position updated: {oldVal:F2} -> {newVal:F2}");
            };

            // Example 3: Batch syncing with AutoSync disabled
            // Make multiple changes, then sync all at once
            var batchOptions = new NetworkSyncOptions
            {
                AutoSync = false,
                KeyPrefix = "Batch_"
            };

            _gamePhase = _client.CreateHostSyncVar("GamePhase", "Lobby", batchOptions);
            _roundNumber = _client.CreateHostSyncVar("RoundNumber", 0, batchOptions);

            _gamePhase.OnValueChanged += (oldVal, newVal) =>
            {
                Console.WriteLine($"[AdvancedExample] Game phase: {oldVal} -> {newVal}");
            };

            _roundNumber.OnValueChanged += (oldVal, newVal) =>
            {
                Console.WriteLine($"[AdvancedExample] Round number: {oldVal} -> {newVal}");
            };

            // Example 4: Custom validator for team names
            // Team names must be 3-15 characters and alphanumeric
            var teamNameValidator = new PredicateValidator<string>(
                value => !string.IsNullOrWhiteSpace(value) && 
                         value.Length >= 3 && 
                         value.Length <= 15 &&
                         IsAlphanumeric(value),
                "Team name must be 3-15 alphanumeric characters"
            );

            _teamName = _client.CreateClientSyncVar(
                "TeamName",
                "Team1",
                new NetworkSyncOptions { WarnOnIgnoredWrites = true },
                teamNameValidator
            );

            _teamName.OnMyValueChanged += (oldVal, newVal) =>
            {
                Console.WriteLine($"[AdvancedExample] Team name changed: {oldVal} -> {newVal}");
            };

            _teamName.OnSyncError += (ex) =>
            {
                Console.WriteLine($"[AdvancedExample] Team name validation failed: {ex.Message}");
            };
        }

        private bool IsAlphanumeric(string value)
        {
            foreach (char c in value)
            {
                if (!char.IsLetterOrDigit(c))
                    return false;
            }
            return true;
        }

        public void Update()
        {
            // Hotkey handlers for testing

            // F1: Set valid score
            if (IsKeyPressed("F1"))
            {
                if (_playerScore != null && _client.IsHost)
                {
                    _playerScore.Value = 1337;
                    Console.WriteLine("[AdvancedExample] F1: Set score to 1337 (valid)");
                }
            }

            // F2: Attempt invalid score
            if (IsKeyPressed("F2"))
            {
                if (_playerScore != null && _client.IsHost)
                {
                    _playerScore.Value = 99999; // Invalid - exceeds max
                    Console.WriteLine("[AdvancedExample] F2: Attempted to set score to 99999 (should fail validation)");
                }
            }

            // F3: Rapid position updates (rate limited)
            if (IsKeyPressed("F3"))
            {
                if (_playerPositionX != null)
                {
                    Console.WriteLine("[AdvancedExample] F3: Simulating rapid position updates...");
                    
                    // Simulate 50 rapid updates - rate limiter will throttle to 10/sec
                    for (int i = 0; i < 50; i++)
                    {
                        _playerPositionX.Value = i * 0.1f;
                    }
                    
                    Console.WriteLine($"[AdvancedExample] Sent 50 position updates. IsDirty: {_playerPositionX.IsDirty}");
                    Console.WriteLine("[AdvancedExample] Rate limiter will sync at max 10/sec. Use F7 to flush immediately.");
                }
            }

            // F4: Batch sync demonstration
            if (IsKeyPressed("F4"))
            {
                if (_gamePhase != null && _roundNumber != null && _client.IsHost)
                {
                    Console.WriteLine("[AdvancedExample] F4: Making multiple changes (batch mode)...");
                    
                    // Make multiple changes without syncing
                    _gamePhase.Value = "InGame";
                    _roundNumber.Value = 1;
                    
                    Console.WriteLine($"[AdvancedExample] GamePhase dirty: {_gamePhase.IsDirty}");
                    Console.WriteLine($"[AdvancedExample] RoundNumber dirty: {_roundNumber.IsDirty}");
                    Console.WriteLine("[AdvancedExample] Changes are pending. Use F7 to flush.");
                }
            }

            // F5: Custom validator test
            if (IsKeyPressed("F5"))
            {
                if (_teamName != null)
                {
                    // Try valid name
                    _teamName.Value = "Alpha";
                    Console.WriteLine("[AdvancedExample] F5: Set team name to 'Alpha' (valid)");
                    
                    // Try invalid names
                    System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                    {
                        _teamName.Value = "X"; // Too short
                        Console.WriteLine("[AdvancedExample] Attempted 'X' (too short - should fail)");
                    });
                    
                    System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
                    {
                        _teamName.Value = "Team@123"; // Invalid characters
                        Console.WriteLine("[AdvancedExample] Attempted 'Team@123' (invalid chars - should fail)");
                    });
                }
            }

            // F6: Check dirty status
            if (IsKeyPressed("F6"))
            {
                Console.WriteLine("[AdvancedExample] F6: Checking dirty status...");
                Console.WriteLine($"  PlayerScore dirty: {_playerScore?.IsDirty ?? false}");
                Console.WriteLine($"  PositionX dirty: {_playerPositionX?.IsDirty ?? false}");
                Console.WriteLine($"  GamePhase dirty: {_gamePhase?.IsDirty ?? false}");
                Console.WriteLine($"  RoundNumber dirty: {_roundNumber?.IsDirty ?? false}");
                Console.WriteLine($"  TeamName dirty: {_teamName?.IsDirty ?? false}");
            }

            // F7: Flush all pending changes
            if (IsKeyPressed("F7"))
            {
                Console.WriteLine("[AdvancedExample] F7: Flushing all pending changes...");
                _playerScore?.FlushPending();
                _playerPositionX?.FlushPending();
                _gamePhase?.FlushPending();
                _roundNumber?.FlushPending();
                _teamName?.FlushPending();
                Console.WriteLine("[AdvancedExample] All pending changes synced!");
            }
        }

        // Placeholder for key input - replace with actual input system
        private bool IsKeyPressed(string key)
        {
            // In a real implementation, this would check actual keyboard input
            // For MelonLoader: return Input.GetKeyDown(KeyCode.F1);
            // For Unity: return UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F1);
            return false;
        }

        public void Cleanup()
        {
            // SyncVars are automatically disposed when leaving lobby
            // No manual cleanup needed!
        }
    }

    /// <summary>
    /// Example of a composite validator that combines multiple validation rules.
    /// </summary>
    public class UsernameValidator : ISyncValidator<string>
    {
        private readonly CompositeValidator<string> _validator;

        public UsernameValidator()
        {
            // Username must be:
            // 1. Between 3 and 20 characters
            // 2. Start with a letter
            // 3. Contain only letters, numbers, and underscores

            _validator = new CompositeValidator<string>(
                new PredicateValidator<string>(
                    value => !string.IsNullOrWhiteSpace(value) && value.Length >= 3 && value.Length <= 20,
                    "Username must be between 3 and 20 characters"
                ),
                new PredicateValidator<string>(
                    value => !string.IsNullOrWhiteSpace(value) && char.IsLetter(value[0]),
                    "Username must start with a letter"
                ),
                new PredicateValidator<string>(
                    value => IsValidUsernameChars(value),
                    "Username can only contain letters, numbers, and underscores"
                )
            );
        }

        public bool IsValid(string value) => _validator.IsValid(value);

        public string? GetErrorMessage(string value) => _validator.GetErrorMessage(value);

        private bool IsValidUsernameChars(string value)
        {
            foreach (char c in value)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }
            return true;
        }
    }
}
