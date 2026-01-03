using System;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using Xunit;

namespace SteamNetworkLib.Tests.TestUtilities
{
    /// <summary>
    /// Shared fixture that initializes Steam API once and reuses it across all integration tests.
    /// Goldberg Steam Emulator only supports one Steam instance per process.
    /// </summary>
    public class SteamTestFixture : IDisposable
    {
        private static readonly object _lock = new object();
        private static SteamTestFixture? _instance;
        private static int _refCount;
        
        private readonly CancellationTokenSource _callbackCts;
        private Task? _callbackTask;
        private bool _disposed;

        /// <summary>
        /// Gets the shared SteamNetworkClient instance.
        /// </summary>
        public SteamNetworkClient? Client { get; private set; }

        /// <summary>
        /// Gets whether Steam is initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Gets the Steam ID of the current user.
        /// </summary>
        public CSteamID SteamId { get; private set; }

        /// <summary>
        /// Gets the singleton instance of the test fixture.
        /// </summary>
        public static SteamTestFixture Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new SteamTestFixture();
                        _instance.Initialize();
                    }
                    _refCount++;
                    return _instance;
                }
            }
        }

        private SteamTestFixture()
        {
            _callbackCts = new CancellationTokenSource();
        }

        private void Initialize()
        {
            if (IsInitialized)
                return;

            try
            {
                var steamId = GoldbergTestHelper.GenerateTestSteamId(1);
                
                // Initialize Goldberg emulator
                if (!GoldbergTestHelper.InitializeGoldberg(steamId, "TestPlayer"))
                {
                    Console.WriteLine("[SteamTestFixture] Failed to initialize Goldberg");
                    return;
                }

                // Small delay to let Steam API initialize
                Thread.Sleep(100);

                // Create SteamNetworkLib client
                Client = new SteamNetworkClient();
                if (!Client.Initialize())
                {
                    Console.WriteLine("[SteamTestFixture] Failed to initialize SteamNetworkLib");
                    return;
                }

                SteamId = SteamUser.GetSteamID();

                // Start callback processing task
                _callbackTask = Task.Run(async () =>
                {
                    while (!_callbackCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            GoldbergTestHelper.ProcessCallbacks();
                            Client?.ProcessIncomingMessages();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SteamTestFixture] Callback error: {ex.Message}");
                        }
                        await Task.Delay(16); // ~60 fps
                    }
                });

                IsInitialized = true;
                Console.WriteLine($"[SteamTestFixture] Initialized (Steam ID: {SteamId.m_SteamID})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamTestFixture] Initialization error: {ex.Message}");
            }
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
                Console.WriteLine($"[SteamTestFixture] Created lobby: {lobby.LobbyId.m_SteamID}");
                return lobby.LobbyId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamTestFixture] Failed to create lobby: {ex.Message}");
                return CSteamID.Nil;
            }
        }

        /// <summary>
        /// Leaves the current lobby if in one.
        /// </summary>
        public void LeaveLobby()
        {
            if (Client != null && Client.IsInLobby)
            {
                Client.LeaveLobby();
                Console.WriteLine("[SteamTestFixture] Left lobby");
            }
        }

        /// <summary>
        /// Releases a reference to the fixture.
        /// </summary>
        public void Release()
        {
            lock (_lock)
            {
                _refCount--;
                if (_refCount <= 0 && _instance != null)
                {
                    _instance.Dispose();
                    _instance = null;
                    _refCount = 0;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Console.WriteLine("[SteamTestFixture] Disposing...");

            _callbackCts.Cancel();
            
            try
            {
                _callbackTask?.Wait(1000);
            }
            catch { }

            Client?.Dispose();
            GoldbergTestHelper.Shutdown();
            GoldbergTestHelper.CleanupConfigFiles();

            _disposed = true;
        }
    }

    /// <summary>
    /// xUnit collection definition to prevent parallel execution of Steam integration tests.
    /// </summary>
    [CollectionDefinition("Steam Integration Tests", DisableParallelization = true)]
    public class SteamTestCollection : ICollectionFixture<SteamCollectionFixture>
    {
    }

    /// <summary>
    /// Collection fixture that manages Steam initialization for all tests in the collection.
    /// </summary>
    public class SteamCollectionFixture : IDisposable
    {
        public SteamTestFixture Fixture { get; }

        public SteamCollectionFixture()
        {
            Fixture = SteamTestFixture.Instance;
        }

        public void Dispose()
        {
            Fixture.Release();
        }
    }
}
