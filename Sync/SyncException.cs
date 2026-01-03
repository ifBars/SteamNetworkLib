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

    /// <summary>
    /// Exception thrown when value validation fails before sync.
    /// </summary>
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

    /// <summary>
    /// Exception thrown when a sync operation fails due to network or state issues.
    /// </summary>
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
