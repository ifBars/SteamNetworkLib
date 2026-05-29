using System;

namespace SteamNetworkLib.Exceptions
{
    /// <summary>
    /// Exception thrown when a SyncVar or Steam data synchronization operation fails.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exception is part of the SteamNetworkLib exception family, so callers can catch
    /// <see cref="SteamNetworkException"/> for all library-level failures or catch
    /// <see cref="SyncException"/> when they need synchronization-specific details.
    /// </para>
    /// <para>
    /// The <see cref="SyncKey"/> property is populated when the failed operation is tied to
    /// a specific lobby, member, host, or client sync key.
    /// </para>
    /// </remarks>
    public class SyncException : SteamNetworkException
    {
        /// <summary>
        /// Gets the key associated with the sync operation, if available.
        /// </summary>
        public string? SyncKey { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SyncException(string message)
            : base(message, SteamNetworkErrorKind.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that caused the current exception.</param>
        public SyncException(string message, Exception innerException)
            : base(message, innerException, SteamNetworkErrorKind.Unknown)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class with key information.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        public SyncException(string message, string syncKey)
            : base(message, SteamNetworkErrorKind.Unknown, operation: null)
        {
            SyncKey = syncKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class with full details.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        /// <param name="innerException">The exception that caused the current exception.</param>
        public SyncException(string message, string syncKey, Exception innerException)
            : base(message, innerException, SteamNetworkErrorKind.Unknown)
        {
            SyncKey = syncKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class with structured diagnostic details.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        /// <param name="errorKind">The broad reason the operation failed.</param>
        /// <param name="operation">The API operation or lifecycle step that failed.</param>
        /// <param name="isRetryable">True when retrying later may succeed; otherwise, false.</param>
        public SyncException(
            string message,
            string? syncKey,
            SteamNetworkErrorKind errorKind,
            string? operation = null,
            bool isRetryable = false)
            : base(message, errorKind, operation, isRetryable)
        {
            SyncKey = syncKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncException"/> class with structured diagnostic details and an inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        /// <param name="innerException">The exception that caused the current exception.</param>
        /// <param name="errorKind">The broad reason the operation failed.</param>
        /// <param name="operation">The API operation or lifecycle step that failed.</param>
        /// <param name="isRetryable">True when retrying later may succeed; otherwise, false.</param>
        public SyncException(
            string message,
            string? syncKey,
            Exception innerException,
            SteamNetworkErrorKind errorKind,
            string? operation = null,
            bool isRetryable = false)
            : base(message, innerException, errorKind, operation, isRetryable)
        {
            SyncKey = syncKey;
        }
    }
}
