using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Compatibility alias for <see cref="Exceptions.SyncException"/>.
    /// </summary>
    /// <remarks>
    /// New code should use <see cref="Exceptions.SyncException"/> from the
    /// <c>SteamNetworkLib.Exceptions</c> namespace so all library exceptions share the same
    /// base type and diagnostic properties.
    /// </remarks>
    [Obsolete("Use SteamNetworkLib.Exceptions.SyncException instead.")]
    public class SyncException : Exceptions.SyncException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SyncException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that caused the current exception.</param>
        public SyncException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class with key information.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        public SyncException(string message, string syncKey)
            : base(message, syncKey)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class with full details.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        /// <param name="innerException">The exception that caused the current exception.</param>
        public SyncException(string message, string syncKey, Exception innerException)
            : base(message, syncKey, innerException)
        {
        }
    }
}
