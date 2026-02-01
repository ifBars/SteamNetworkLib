using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Exception thrown when serialization or deserialization fails in sync operations.
    /// </summary>
    /// <remarks>
    /// <para>This exception is thrown when:</para>
    /// <list type="bullet">
    /// <item><description>A type cannot be serialized (unsupported type)</description></item>
    /// <item><description>Serialization fails due to circular references or other issues</description></item>
    /// <item><description>Deserialization fails due to malformed data or type mismatch</description></item>
    /// </list>
    /// <para>This exception provides detailed information about the type that failed to serialize/deserialize and the associated sync key when available.</para>
    /// </remarks>
    public class SyncSerializationException : Exception
    {
        /// <summary>
        /// Gets the type that failed to serialize/deserialize, if available.
        /// </summary>
        public Type? TargetType { get; }

        /// <summary>
        /// Gets the key associated with the sync operation, if available.
        /// </summary>
        public string? SyncKey { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncSerializationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public SyncSerializationException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncSerializationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception that caused this exception.</param>
        public SyncSerializationException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncSerializationException"/> class
        /// with type information.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="targetType">The type that failed to serialize/deserialize.</param>
        public SyncSerializationException(string message, Type targetType) 
            : base(message)
        {
            TargetType = targetType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncSerializationException"/> class
        /// with type and key information.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="targetType">The type that failed to serialize/deserialize.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        public SyncSerializationException(string message, Type targetType, string syncKey) 
            : base(message)
        {
            TargetType = targetType;
            SyncKey = syncKey;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncSerializationException"/> class
        /// with full details.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="targetType">The type that failed to serialize/deserialize.</param>
        /// <param name="syncKey">The sync key associated with the operation.</param>
        /// <param name="innerException">The inner exception that caused this exception.</param>
        public SyncSerializationException(string message, Type targetType, string syncKey, Exception innerException) 
            : base(message, innerException)
        {
            TargetType = targetType;
            SyncKey = syncKey;
        }
    }
}