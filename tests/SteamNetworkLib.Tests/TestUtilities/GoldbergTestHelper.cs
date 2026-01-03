using System;
using System.IO;
using System.Text;
using Steamworks;

namespace SteamNetworkLib.Tests.TestUtilities
{
    /// <summary>
    /// Helper class for configuring and managing Goldberg Steam Emulator for tests.
    /// </summary>
    public static class GoldbergTestHelper
    {
        /// <summary>
        /// Default Steam App ID for testing (480 = Spacewar, the standard testing app).
        /// </summary>
        public const uint DEFAULT_APP_ID = 480;

        /// <summary>
        /// Initializes a Goldberg emulator instance with a specific Steam ID for testing.
        /// </summary>
        /// <param name="steamId">The Steam ID to use for this test instance.</param>
        /// <param name="playerName">The display name for this player.</param>
        /// <param name="appId">The Steam App ID to use (defaults to 480 - Spacewar).</param>
        /// <returns>True if initialization was successful.</returns>
        public static bool InitializeGoldberg(ulong steamId, string playerName, uint appId = DEFAULT_APP_ID)
        {
            try
            {
                // Create steam_appid.txt if it doesn't exist
                CreateAppIdFile(appId);

                // Create account_name.txt for player name
                CreateAccountNameFile(playerName);

                // Create user_steam_id.txt for specific Steam ID
                CreateSteamIdFile(steamId);

                // Create force_account_name.txt to force the account name
                CreateForceAccountNameFile(playerName);

                // Create settings/configs.main.txt for network settings
                CreateMainConfigFile();

                // Initialize Steam API
                return SteamAPI.Init();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize Goldberg: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Shuts down the Steam API.
        /// </summary>
        public static void Shutdown()
        {
            try
            {
                SteamAPI.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error shutting down Steam API: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the steam_appid.txt file required by Goldberg.
        /// </summary>
        private static void CreateAppIdFile(uint appId)
        {
            var appIdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steam_appid.txt");
            WriteFileWithRetry(appIdPath, appId.ToString());
        }

        /// <summary>
        /// Creates the account_name.txt file for player display name.
        /// </summary>
        private static void CreateAccountNameFile(string playerName)
        {
            var accountNamePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "account_name.txt");
            WriteFileWithRetry(accountNamePath, playerName);
        }

        /// <summary>
        /// Creates the user_steam_id.txt file to set a specific Steam ID.
        /// </summary>
        private static void CreateSteamIdFile(ulong steamId)
        {
            var steamIdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_steam_id.txt");
            WriteFileWithRetry(steamIdPath, steamId.ToString());
        }

        /// <summary>
        /// Creates the force_account_name.txt file.
        /// </summary>
        private static void CreateForceAccountNameFile(string playerName)
        {
            var forceAccountPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "force_account_name.txt");
            WriteFileWithRetry(forceAccountPath, playerName);
        }

        /// <summary>
        /// Creates the main configuration file for Goldberg network settings.
        /// </summary>
        private static void CreateMainConfigFile()
        {
            var settingsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
            Directory.CreateDirectory(settingsDir);

            var configPath = Path.Combine(settingsDir, "configs.main.txt");
            
            // Configuration for local network lobbies
            var config = new StringBuilder();
            config.AppendLine("# Goldberg Steam Emulator Configuration");
            config.AppendLine("# Enable local network play");
            config.AppendLine("enable_lan_only_mode=1");
            config.AppendLine("");
            config.AppendLine("# Disable Steam overlay (not needed for tests)");
            config.AppendLine("disable_overlay=1");
            config.AppendLine("");
            config.AppendLine("# Enable local save (for lobby data persistence)");
            config.AppendLine("enable_local_save=1");
            config.AppendLine("");
            config.AppendLine("# Network settings");
            config.AppendLine("listen_port=47584");

            WriteFileWithRetry(configPath, config.ToString());
        }

        /// <summary>
        /// Writes a file with retry logic to handle concurrent access.
        /// </summary>
        private static void WriteFileWithRetry(string path, string content, int maxRetries = 5)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Use FileShare.ReadWrite to allow concurrent access
                    using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        writer.Write(content);
                    }
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    // File is locked, wait and retry
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        /// <summary>
        /// Generates a unique test Steam ID based on process ID and counter.
        /// </summary>
        /// <param name="counter">A counter to differentiate between test instances.</param>
        /// <returns>A unique Steam ID for testing.</returns>
        public static ulong GenerateTestSteamId(int counter = 0)
        {
            // Generate a Steam ID in the valid range for individual accounts
            // Steam IDs for individual accounts start at 76561197960265728
            // We add a counter to ensure uniqueness across test runs
            var baseId = 76561197960265728UL;
            var processId = (ulong)System.Diagnostics.Process.GetCurrentProcess().Id;
            var timestamp = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond) % 10000;
            
            return baseId + (processId * 10000) + timestamp + (ulong)counter;
        }

        /// <summary>
        /// Waits for Steam callbacks to be processed.
        /// Call this regularly in test loops to process Steam events.
        /// </summary>
        public static void ProcessCallbacks()
        {
            SteamAPI.RunCallbacks();
        }

        /// <summary>
        /// Creates a lobby info file for joining existing lobbies.
        /// This is useful for multi-process tests where one process creates a lobby
        /// and others need to join it.
        /// </summary>
        /// <param name="lobbyId">The lobby ID to join.</param>
        /// <param name="filePath">Path to save the lobby info file.</param>
        public static void SaveLobbyInfo(CSteamID lobbyId, string filePath)
        {
            File.WriteAllText(filePath, lobbyId.m_SteamID.ToString());
        }

        /// <summary>
        /// Loads lobby info from a file created by SaveLobbyInfo.
        /// </summary>
        /// <param name="filePath">Path to the lobby info file.</param>
        /// <returns>The lobby Steam ID, or CSteamID.Nil if file doesn't exist.</returns>
        public static CSteamID LoadLobbyInfo(string filePath)
        {
            if (!File.Exists(filePath))
                return CSteamID.Nil;

            var idText = File.ReadAllText(filePath).Trim();
            if (ulong.TryParse(idText, out var lobbyId))
            {
                return new CSteamID(lobbyId);
            }

            return CSteamID.Nil;
        }

        /// <summary>
        /// Cleans up all Goldberg configuration files.
        /// </summary>
        public static void CleanupConfigFiles()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var filesToDelete = new[]
                {
                    "steam_appid.txt",
                    "account_name.txt",
                    "user_steam_id.txt",
                    "force_account_name.txt"
                };

                foreach (var file in filesToDelete)
                {
                    var path = Path.Combine(baseDir, file);
                    if (File.Exists(path))
                        File.Delete(path);
                }

                // Clean up settings directory
                var settingsDir = Path.Combine(baseDir, "settings");
                if (Directory.Exists(settingsDir))
                    Directory.Delete(settingsDir, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up config files: {ex.Message}");
            }
        }
    }
}
