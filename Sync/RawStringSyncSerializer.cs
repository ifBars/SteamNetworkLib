using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Sync serializer that passes string values through without JSON encoding.
    /// </summary>
    /// <remarks>
    /// Use this for pre-serialized payloads, pipe-delimited state, compact protocol strings,
    /// or compatibility data that must match another mod's existing lobby-data format. Non-empty
    /// strings are stored verbatim. Steam lobby and member data use an empty string for missing keys,
    /// so an empty or null value follows the SyncVar's missing-value behavior and resolves to its
    /// configured default on other peers.
    /// It only supports <see cref="string"/> values; use <see cref="JsonSyncSerializer"/>
    /// for typed objects, primitives, collections, and general-purpose SyncVars.
    /// </remarks>
    public sealed class RawStringSyncSerializer : ISyncSerializer
    {
        /// <inheritdoc />
        public string Serialize<T>(T value)
        {
            EnsureStringType(typeof(T));
            return value as string ?? string.Empty;
        }

        /// <inheritdoc />
        public T Deserialize<T>(string data)
        {
            EnsureStringType(typeof(T));
            return (T)(object)(data ?? string.Empty);
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
