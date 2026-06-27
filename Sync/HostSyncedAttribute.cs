using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Marks a field or property as host-authoritative synchronized state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Members marked with this attribute can be bound with
    /// <see cref="NetworkSyncBinder.BindHostSynced(SteamNetworkLib.SteamNetworkClient, object, NetworkSyncOptions?)"/>.
    /// The binder creates a regular <see cref="HostSyncVar{T}"/> for each marked member, so the normal
    /// host-only authority, serialization, prefix, and rate-limit behavior still applies.
    /// </para>
    /// <para>
    /// This attribute is intended for compact host-owned values, not Unity objects, live scene references,
    /// per-frame transforms, local preferences, or client-owned state.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class HostSyncedAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HostSyncedAttribute"/> class.
        /// </summary>
        public HostSyncedAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HostSyncedAttribute"/> class with an explicit sync key.
        /// </summary>
        /// <param name="key">The sync key to use instead of the member name.</param>
        public HostSyncedAttribute(string key)
        {
            Key = key;
        }

        /// <summary>
        /// Gets the explicit sync key, or null to use the member name.
        /// </summary>
        public string? Key { get; }
    }
}
