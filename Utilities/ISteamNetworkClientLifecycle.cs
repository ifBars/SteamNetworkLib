using SteamNetworkLib.Exceptions;

namespace SteamNetworkLib.Utilities
{
    /// <summary>
    /// Minimal lifecycle surface needed by <see cref="SteamNetworkClientRunner"/>.
    /// </summary>
    public interface ISteamNetworkClientLifecycle
    {
        /// <summary>
        /// Gets a value indicating whether networking has initialized successfully.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Attempts to initialize networking without throwing for expected Steamworks availability failures.
        /// </summary>
        /// <param name="error">The initialization error when initialization fails.</param>
        /// <returns>True when initialization succeeds; otherwise, false.</returns>
        bool TryInitialize(out SteamNetworkException? error);

        /// <summary>
        /// Processes pending incoming network messages.
        /// </summary>
        void ProcessIncomingMessages();

        /// <summary>
        /// Releases network resources.
        /// </summary>
        void Dispose();
    }
}
