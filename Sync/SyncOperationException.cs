using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Exception thrown when a sync operation fails due to network or state issues.
    /// </summary>
    /// <remarks>
    /// <para>This exception is thrown when:</para>
    /// <list type="bullet">
    /// <item><description>Network connectivity issues prevent sync operations</description></item>
    /// <item><description>Steam network state is invalid or disconnected</description></item>
    /// <item><description>Sync operations fail due to internal Steamworks API errors</description></item>
    /// <item><description>Operation timeouts occur during sync</description></item>
    /// </list>
    /// <para>This exception provides information about the sync key when available and wraps any underlying Steamworks API exceptions.</para>
    /// </remarks>
    public class SyncException : Exception
    {
        /// <summary>
        /// Gets the key associated with the sync operation, if available.
        /// </summary>
        public string? SyncKey { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public SyncException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception that caused this exception.</param>
        public SyncException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class
        /// with key information.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        public SyncException(string message, string syncKey) 
            : base(message)
        {
            SyncKey = syncKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class
        /// with full details.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        /// <param name="innerException">The inner exception that caused this exception.</param>
        public SyncException(string message, string syncKey, Exception innerException) 
            : base(message, innerException)
        {
            SyncKey = syncKey;
        }
    }
}