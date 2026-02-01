using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Exception thrown when value validation fails before sync.
    /// </summary>
    /// <remarks>
    /// <para>This exception is thrown when:</para>
    /// <list type="bullet">
    /// <item><description>A value fails validation rules before being synced</description></item>
    /// <item><description>The value is null when a non-null value is required</description></item>
    /// <item><description>The value type does not match the expected type for the sync key</description></item>
    /// </list>
    /// <para>This exception provides information about the invalid value and the associated sync key when available.</para>
    /// </remarks>
    public class SyncValidationException : Exception
    {
        /// <summary>
        /// Gets the key associated with the sync operation, if available.
        /// </summary>
        public string? SyncKey { get; }

        /// <summary>
        /// Gets the invalid value, if available.
        /// </summary>
        public object? InvalidValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncValidationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public SyncValidationException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncValidationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        /// <param name="invalidValue">The value that failed validation.</param>
        public SyncValidationException(string message, string syncKey, object? invalidValue) 
            : base(message)
        {
            SyncKey = syncKey;
            InvalidValue = invalidValue;
        }
    }
}