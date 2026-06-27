using System;

namespace SteamNetworkLib.Utilities
{
    /// <summary>
    /// Configures <see cref="SteamNetworkClientRunner"/> retry and message-pump behavior.
    /// </summary>
    public sealed class SteamNetworkClientRunnerOptions
    {
        /// <summary>
        /// Gets or sets how long to wait between failed initialization attempts.
        /// </summary>
        public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Gets or sets whether <see cref="SteamNetworkClientRunner.Tick"/> should retry until initialization succeeds.
        /// </summary>
        public bool RetryUntilInitialized { get; set; } = true;

        /// <summary>
        /// Gets or sets whether <see cref="SteamNetworkClientRunner.Tick"/> should call
        /// <see cref="ISteamNetworkClientLifecycle.ProcessIncomingMessages"/> after initialization succeeds.
        /// </summary>
        public bool ProcessMessagesWhenInitialized { get; set; } = true;
    }
}
