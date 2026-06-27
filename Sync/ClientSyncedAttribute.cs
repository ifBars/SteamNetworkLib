using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Marks a field or property as local-player-owned synchronized state.
    /// </summary>
    /// <remarks>
    /// Members marked with this attribute can be bound with
    /// <see cref="NetworkSyncBinder.BindSynced(SteamNetworkLib.SteamNetworkClient, object, NetworkSyncOptions?)"/>.
    /// The binder creates a regular <see cref="ClientSyncVar{T}"/> for each marked member, so each
    /// client owns its own value while other lobby members can read it through the SyncVar API.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ClientSyncedAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientSyncedAttribute"/> class.
        /// </summary>
        public ClientSyncedAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientSyncedAttribute"/> class with an explicit sync key.
        /// </summary>
        /// <param name="key">The sync key to use instead of the member name.</param>
        public ClientSyncedAttribute(string key)
        {
            Key = key;
        }

        /// <summary>
        /// Gets the explicit sync key, or null to use the member name.
        /// </summary>
        public string? Key { get; }
    }
}
