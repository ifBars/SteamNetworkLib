using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Sync serializer that stores string values exactly as provided, without JSON quoting.
    /// </summary>
    /// <remarks>
    /// Use this for non-empty pre-serialized payloads, pipe-delimited state, compact protocol strings,
    /// or compatibility data that must match another mod's existing lobby-data format. Empty strings
    /// are stored with an internal sentinel so they can round-trip through Steam APIs that return an
    /// empty string for missing keys.
    /// It only supports <see cref="string"/> values; use <see cref="JsonSyncSerializer"/>
    /// for typed objects, primitives, collections, and general-purpose SyncVars.
    /// </remarks>
    public sealed class RawStringSyncSerializer : ISyncSerializer
    {
        private const string EmptyStringSentinel = "\uE000SteamNetworkLib.EmptyRawString";

        /// <inheritdoc />
        public string Serialize<T>(T value)
        {
            EnsureStringType(typeof(T));
            var raw = value as string;
            return string.IsNullOrEmpty(raw) ? EmptyStringSentinel : raw;
        }

        /// <inheritdoc />
        public T Deserialize<T>(string data)
        {
            EnsureStringType(typeof(T));
            return (T)(object)(data == EmptyStringSentinel ? string.Empty : data ?? string.Empty);
        }

        /// <inheritdoc />
        public bool CanSerialize(Type type)
        {
            return type == typeof(string);
        }

        private static void EnsureStringType(Type type)
        {
            if (type != typeof(string))
            {
                throw new SyncSerializationException(
                    $"{nameof(RawStringSyncSerializer)} only supports string values.",
                    type);
            }
        }
    }
}
