using System;

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// Configuration options for <see cref="HostSyncVar{T}"/> and <see cref="ClientSyncVar{T}"/> behavior.
    /// All properties have sensible defaults for the simplest developer experience.
    /// </summary>
    /// <remarks>
    /// <para>This class follows the same pattern as <see cref="Core.NetworkRules"/> - providing
    /// high-level abstractions with sensible defaults while allowing advanced configuration.</para>
    /// <example>
    /// <code>
    /// // Simple usage with defaults
    /// var score = client.CreateHostSyncVar("Score", 0);
    /// 
    /// // Advanced usage with custom options
    /// var options = new NetworkSyncOptions
    /// {
    ///     KeyPrefix = "MyMod_",
    ///     WarnOnIgnoredWrites = true
    /// };
    /// var settings = client.CreateHostSyncVar("Settings", new GameSettings(), options);
    /// </code>
    /// </example>
    /// </remarks>
    public class NetworkSyncOptions
    {
        /// <summary>
        /// If true, automatically syncs value changes when the value is set.
        /// Default: true
        /// </summary>
        /// <remarks>
        /// When enabled, setting <c>Value</c> immediately propagates the change to all connected clients.
        /// Disable this if you need to batch multiple changes before syncing manually with <c>Flush()</c>.
        /// </remarks>
        public bool AutoSync { get; set; } = true;

        /// <summary>
        /// If true, syncs current value to newly joined players.
        /// Default: true
        /// </summary>
        /// <remarks>
        /// This ensures late-joining players receive the current state.
        /// For <see cref="HostSyncVar{T}"/>, the host re-broadcasts the value.
        /// For <see cref="ClientSyncVar{T}"/>, each player's value is already available via Steam member data.
        /// </remarks>
        public bool SyncOnPlayerJoin { get; set; } = true;

        /// <summary>
        /// Maximum number of syncs per second. If 0, no rate limiting is applied.
        /// Default: 0 (unlimited)
        /// </summary>
        /// <remarks>
        /// <para>When set to a value greater than 0, rapid value changes will be throttled
        /// to this maximum rate. Useful for high-frequency updates like player positions.</para>
        /// <para>The latest value is always sent when the rate limit expires.</para>
        /// </remarks>
        public int MaxSyncsPerSecond { get; set; } = 0;

        /// <summary>
        /// If true, validation errors throw exceptions. If false, they are logged and invoke OnSyncError.
        /// Default: false
        /// </summary>
        /// <remarks>
        /// <para>When false (default), validation errors are handled gracefully and don't interrupt execution.</para>
        /// <para>When true, validation errors will throw <see cref="SyncValidationException"/>.</para>
        /// </remarks>
        public bool ThrowOnValidationError { get; set; } = false;

        /// <summary>
        /// If true, logs a warning when a non-host attempts to write to a <see cref="HostSyncVar{T}"/>.
        /// Default: false
        /// </summary>
        /// <remarks>
        /// <para>Useful for debugging during development to catch accidental writes from non-host clients.</para>
        /// <para>The write is always silently ignored regardless of this setting - this only controls logging.</para>
        /// </remarks>
        public bool WarnOnIgnoredWrites { get; set; } = false;

        /// <summary>
        /// Custom serializer for the value type. If null, uses the default <see cref="JsonSyncSerializer"/>.
        /// </summary>
        /// <remarks>
        /// <para>Implement <see cref="ISyncSerializer"/> for custom serialization logic.</para>
        /// <para>The default JSON serializer supports:</para>
        /// <list type="bullet">
        /// <item><description>Primitives: int, float, double, bool, string, long</description></item>
        /// <item><description>Collections: List&lt;T&gt;, Dictionary&lt;string, T&gt;, arrays</description></item>
        /// <item><description>Custom types with parameterless constructor and public properties</description></item>
        /// </list>
        /// </remarks>
        public ISyncSerializer? Serializer { get; set; }

        /// <summary>
        /// Key prefix to avoid collisions with other mods using SteamNetworkLib.
        /// Default: null (no prefix)
        /// </summary>
        /// <remarks>
        /// <para>Recommended for published mods to prevent key collisions.</para>
        /// <para>The final key will be: <c>{KeyPrefix}{key}</c></para>
        /// <example>
        /// <code>
        /// var options = new NetworkSyncOptions { KeyPrefix = "MyMod_" };
        /// var score = client.CreateHostSyncVar("Score", 0, options);
        /// // Actual Steam lobby data key: "MyMod_Score"
        /// </code>
        /// </example>
        /// </remarks>
        public string? KeyPrefix { get; set; }

        /// <summary>
        /// Reserved key prefix used internally by SteamNetworkLib.
        /// Keys starting with this prefix are reserved and cannot be used.
        /// </summary>
        internal const string ReservedPrefix = "__snl_sync_";

        /// <summary>
        /// Creates a new instance with default options.
        /// </summary>
        public NetworkSyncOptions()
        {
        }

        /// <summary>
        /// Gets the full key including any prefix.
        /// </summary>
        /// <param name="key">The base key name.</param>
        /// <returns>The full key with prefix applied.</returns>
        internal string GetFullKey(string key)
        {
            if (string.IsNullOrEmpty(KeyPrefix))
            {
                return key;
            }
            return KeyPrefix + key;
        }
    }
}
