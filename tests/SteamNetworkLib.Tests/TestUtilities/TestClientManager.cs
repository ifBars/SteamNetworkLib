using System;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;

namespace SteamNetworkLib.Tests.TestUtilities
{
    /// <summary>
    /// Manages a test Steam client instance for integration testing.
    /// Handles initialization, cleanup, and callback processing.
    /// </summary>
    public class TestClientManager : IDisposable
    {
        private readonly ulong _steamId;
        private readonly string _playerName;
        private readonly CancellationTokenSource _callbackCts;
        private Task? _callbackTask;
        private bool _disposed;

        public SteamNetworkClient? Client { get; private set; }
        public bool IsInitialized { get; private set; }

        public TestClientManager(ulong steamId, string playerName)
        {
            _steamId = steamId;
            _playerName = playerName;
            _callbackCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Initializes the Steam API and SteamNetworkLib client.
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            if (IsInitialized)
                return true;

            // Initialize Goldberg emulator
            if (!GoldbergTestHelper.InitializeGoldberg(_steamId, _playerName))
            {
                Console.WriteLine($"[{_playerName}] Failed to initialize Goldberg");
                return false;
            }

            // Small delay to let Steam API initialize
            await Task.Delay(100);

            // Create SteamNetworkLib client
            Client = new SteamNetworkClient();
            if (!Client.Initialize())
            {
                Console.WriteLine($"[{_playerName}] Failed to initialize SteamNetworkLib");
                return false;
            }

            // Start callback processing task
            _callbackTask = Task.Run(async () =>
            {
                while (!_callbackCts.Token.IsCancellationRequested)
                {
                    GoldbergTestHelper.ProcessCallbacks();
                    Client.ProcessIncomingMessages();
                    await Task.Delay(10); // Process callbacks ~100 times per second
                }
            });

            IsInitialized = true;
            Console.WriteLine($"[{_playerName}] Initialized successfully (Steam ID: {_steamId})");
            return true;
        }

        /// <summary>
        /// Creates a lobby and returns the lobby ID.
        /// </summary>
        public async Task<CSteamID> CreateLobbyAsync(int maxPlayers = 4)
        {
            if (Client == null || !IsInitialized)
                throw new InvalidOperationException("Client not initialized");

            try
            {
                var lobby = await Client.CreateLobbyAsync(ELobbyType.k_ELobbyTypePrivate, maxPlayers);
                Console.WriteLine($"[{_playerName}] Created lobby: {lobby.LobbyId.m_SteamID}");
                return lobby.LobbyId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_playerName}] Failed to create lobby: {ex.Message}");
                return CSteamID.Nil;
            }
        }

        /// <summary>
        /// Joins a lobby by ID.
        /// </summary>
        public async Task<bool> JoinLobbyAsync(CSteamID lobbyId)
        {
            if (Client == null || !IsInitialized)
                throw new InvalidOperationException("Client not initialized");

            try
            {
                var lobby = await Client.JoinLobbyAsync(lobbyId);
                Console.WriteLine($"[{_playerName}] Joined lobby: {lobby.LobbyId.m_SteamID}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_playerName}] Failed to join lobby: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Waits for a specific condition to be true.
        /// </summary>
        public async Task<bool> WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000)
        {
            var startTime = DateTime.UtcNow;
            while (!condition())
            {
                if ((DateTime.UtcNow - startTime).TotalMilliseconds > timeoutMs)
                {
                    Console.WriteLine($"[{_playerName}] Condition wait timed out");
                    return false;
                }

                await Task.Delay(10);
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Console.WriteLine($"[{_playerName}] Disposing...");

            // Stop callback processing
            _callbackCts?.Cancel();
            _callbackTask?.Wait(1000);

            // Cleanup client
            Client?.Dispose();

            // Shutdown Steam API
            GoldbergTestHelper.Shutdown();

            _callbackCts?.Dispose();
            _disposed = true;
        }
    }
}
