using System;
using SteamNetworkLib.Core;
using SteamNetworkLib.Events;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif

namespace SteamNetworkLib.Sync
{
    /// <summary>
    /// A host-authoritative synchronized variable that automatically keeps its value
    /// in sync across all lobby members using Steam lobby data.
    /// </summary>
    /// <typeparam name="T">The type of value to synchronize.</typeparam>
    /// <remarks>
    /// <para><strong>Authority Model:</strong> Only the lobby host can modify this value.
    /// Non-host clients can read the value and observe changes, but writes are silently ignored.</para>
    /// 
    /// <para><strong>Storage:</strong> Uses Steam lobby data for storage, which is automatically
    /// synchronized to all lobby members by Steam.</para>
    /// 
    /// <para><strong>Supported Types:</strong></para>
    /// <list type="bullet">
    /// <item><description>Primitives: int, float, double, bool, string, long, etc.</description></item>
    /// <item><description>Enums: Serialized as underlying integer values</description></item>
    /// <item><description>Collections: List&lt;T&gt;, Dictionary&lt;string, T&gt;, arrays</description></item>
    /// <item><description>Custom types: Classes with parameterless constructor and public properties</description></item>
    /// </list>
    /// 
    /// <example>
    /// <code>
    /// // Create a host-authoritative sync var
    /// var roundNumber = client.CreateHostSyncVar("Round", 1);
    /// 
    /// // Subscribe to changes
    /// roundNumber.OnValueChanged += (oldVal, newVal) => 
    ///     MelonLogger.Msg($"Round changed: {oldVal} -> {newVal}");
    /// 
    /// // Only host can modify - silently ignored for non-hosts
    /// roundNumber.Value = 2;
    /// </code>
    /// </example>
    /// </remarks>
    public class HostSyncVar<T> : IDisposable
    {
        private readonly SteamNetworkClient _client;
        private readonly string _key;
        private readonly string _fullKey;
        private readonly NetworkSyncOptions _options;
        private readonly ISyncSerializer _serializer;
        private readonly ISyncValidator<T>? _validator;
        private T _value;
        private T _defaultValue;
        private bool _disposed;
        private bool _isSubscribed;
        private DateTime _lastSyncTime;
        private T? _pendingValue;
        private bool _hasPendingValue;

        /// <summary>
        /// Gets or sets the synchronized value.
        /// </summary>
        /// <remarks>
        /// <para><strong>Reading:</strong> Always returns the current synchronized value.</para>
        /// <para><strong>Writing:</strong> Only takes effect when called by the lobby host.
        /// Non-host writes are silently ignored (or logged if <see cref="NetworkSyncOptions.WarnOnIgnoredWrites"/> is enabled).</para>
        /// </remarks>
        public T Value
        {
            get => _value;
            set => SetValue(value);
        }

        /// <summary>
        /// Occurs when the value changes from any source (local or remote).
        /// </summary>
        /// <remarks>
        /// Parameters: (oldValue, newValue)
        /// </remarks>
        public event Action<T, T>? OnValueChanged;

        /// <summary>
        /// Occurs when a non-host attempts to write a value.
        /// </summary>
        /// <remarks>
        /// <para>This event is primarily for debugging purposes.</para>
        /// <para>Parameter: the attempted value that was ignored</para>
        /// </remarks>
        public event Action<T>? OnWriteIgnored;

        /// <summary>
        /// Occurs when a sync operation fails (serialization error, etc.).
        /// </summary>
        public event Action<Exception>? OnSyncError;

        /// <summary>
        /// Gets whether the local player can modify this value (i.e., is host).
        /// </summary>
        public bool CanWrite => _client.IsHost;

        /// <summary>
        /// Gets the sync key used for this variable.
        /// </summary>
        public string Key => _key;

        /// <summary>
        /// Gets the full key including any prefix.
        /// </summary>
        public string FullKey => _fullKey;

        /// <summary>
        /// Gets whether there is a pending value waiting to be synced.
        /// </summary>
        /// <remarks>
        /// This is true when AutoSync is disabled or when rate limiting has deferred a sync.
        /// </remarks>
        public bool IsDirty => _hasPendingValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostSyncVar{T}"/> class.
        /// </summary>
        /// <param name="client">The Steam network client.</param>
        /// <param name="key">The unique key for this sync variable.</param>
        /// <param name="defaultValue">The default value when no synced value exists.</param>
        /// <param name="options">Optional configuration options.</param>
        /// <param name="validator">Optional validator for value constraints.</param>
        /// <exception cref="ArgumentNullException">Thrown when client or key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or invalid.</exception>
        /// <exception cref="SyncSerializationException">Thrown when the type T cannot be serialized.</exception>
        internal HostSyncVar(SteamNetworkClient client, string key, T defaultValue, NetworkSyncOptions? options = null, ISyncValidator<T>? validator = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Sync key cannot be null or empty", nameof(key));
            }

            _key = key;
            _options = options ?? new NetworkSyncOptions();
            _fullKey = _options.GetFullKey(key);
            _defaultValue = defaultValue;
            _value = defaultValue;
            _serializer = _options.Serializer ?? new JsonSyncSerializer();
            _validator = validator;
            _lastSyncTime = DateTime.MinValue;

            // Validate that the type can be serialized
            if (!_serializer.CanSerialize(typeof(T)))
            {
                throw new SyncSerializationException(
                    $"Type '{typeof(T).Name}' cannot be serialized. Ensure it has a parameterless constructor " +
                    "and all public properties are of supported types (primitives, strings, List<T>, Dictionary<string, T>, " +
                    "or other serializable custom types).",
                    typeof(T),
                    _fullKey);
            }

            // Subscribe to lobby data changes
            SubscribeToChanges();

            // Try to load existing value if we're in a lobby
            TryLoadExistingValue();
        }

        private void SetValue(T newValue)
        {
            // Check if we can write
            if (!_client.IsHost)
            {
                // Non-host write - silently ignore
                OnWriteIgnored?.Invoke(newValue);

                if (_options.WarnOnIgnoredWrites)
                {
                    Console.WriteLine($"[SteamNetworkLib] HostSyncVar '{_key}': Write ignored - only host can modify this value.");
                }

                return;
            }

            // Check if value actually changed
            if (Equals(_value, newValue))
            {
                return;
            }

            // Validate the new value
            if (_validator != null && !_validator.IsValid(newValue))
            {
                var errorMsg = _validator.GetErrorMessage(newValue) ?? "Validation failed";
                var fullMsg = $"[SteamNetworkLib] HostSyncVar '{_key}': {errorMsg}";

                if (_options.ThrowOnValidationError)
                {
                    throw new SyncValidationException(fullMsg, _fullKey, newValue);
                }
                else
                {
                    OnSyncError?.Invoke(new SyncValidationException(fullMsg, _fullKey, newValue));
                    Console.WriteLine(fullMsg);
                    return;
                }
            }

            // Check rate limiting
            if (_options.MaxSyncsPerSecond > 0)
            {
                var timeSinceLastSync = DateTime.UtcNow - _lastSyncTime;
                var minInterval = TimeSpan.FromSeconds(1.0 / _options.MaxSyncsPerSecond);

                if (timeSinceLastSync < minInterval)
                {
                    // Store pending value and return - will be synced later
                    _pendingValue = newValue;
                    _hasPendingValue = true;
                    return;
                }
            }

            // Check if AutoSync is enabled
            if (!_options.AutoSync)
            {
                // Mark as dirty but don't sync yet
                _pendingValue = newValue;
                _hasPendingValue = true;
                return;
            }

            PerformSync(newValue);
        }

        private void PerformSync(T newValue)
        {
            var oldValue = _value;

            try
            {
                // Serialize and store
                var serialized = _serializer.Serialize(newValue);
                _client.SetLobbyData(_fullKey, serialized);
                
                // Update local value
                _value = newValue;
                _lastSyncTime = DateTime.UtcNow;
                _hasPendingValue = false;

                // Fire change event
                OnValueChanged?.Invoke(oldValue, newValue);
            }
            catch (Exception ex)
            {
                OnSyncError?.Invoke(ex);
                
                if (_options.WarnOnIgnoredWrites)
                {
                    Console.WriteLine($"[SteamNetworkLib] HostSyncVar '{_key}': Sync error - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Forces immediate sync of any pending value, bypassing rate limit.
        /// </summary>
        /// <remarks>
        /// Use this when AutoSync is disabled to manually trigger sync operations,
        /// or to force a rate-limited pending value to sync immediately.
        /// </remarks>
        public void FlushPending()
        {
            if (_hasPendingValue && _pendingValue != null)
            {
                PerformSync(_pendingValue);
            }
        }

        /// <summary>
        /// Forces a refresh of the value from the lobby data.
        /// </summary>
        /// <remarks>
        /// Normally not needed as changes are automatically detected, but useful
        /// after joining a lobby or when debugging synchronization issues.
        /// </remarks>
        public void Refresh()
        {
            TryLoadExistingValue();
        }

        private void TryLoadExistingValue()
        {
            if (!_client.IsInLobby)
            {
                return;
            }

            try
            {
                var serialized = _client.GetLobbyData(_fullKey);
                if (!string.IsNullOrEmpty(serialized))
                {
                    var oldValue = _value;
                    _value = _serializer.Deserialize<T>(serialized);
                    
                    // Only fire event if value actually changed
                    if (!Equals(oldValue, _value))
                    {
                        OnValueChanged?.Invoke(oldValue, _value);
                    }
                }
            }
            catch (Exception ex)
            {
                // Failed to load - keep default value
                OnSyncError?.Invoke(ex);
            }
        }

        private void SubscribeToChanges()
        {
            if (_isSubscribed) return;

            _client.OnLobbyDataChanged += HandleLobbyDataChanged;
            _client.OnLobbyJoined += HandleLobbyJoined;
            _isSubscribed = true;
        }

        private void UnsubscribeFromChanges()
        {
            if (!_isSubscribed) return;

            _client.OnLobbyDataChanged -= HandleLobbyDataChanged;
            _client.OnLobbyJoined -= HandleLobbyJoined;
            _isSubscribed = false;
        }

        private void HandleLobbyDataChanged(object? sender, LobbyDataChangedEventArgs e)
        {
            // Only process changes for our key
            if (e.Key != _fullKey) return;

            try
            {
                if (string.IsNullOrEmpty(e.NewValue))
                {
                    // Value was removed - reset to default
                    var oldValue = _value;
                    _value = _defaultValue;
                    
                    if (!Equals(oldValue, _value))
                    {
                        OnValueChanged?.Invoke(oldValue, _value);
                    }
                }
                else
                {
                    var oldValue = _value;
                    _value = _serializer.Deserialize<T>(e.NewValue);
                    
                    if (!Equals(oldValue, _value))
                    {
                        OnValueChanged?.Invoke(oldValue, _value);
                    }
                }
            }
            catch (Exception ex)
            {
                OnSyncError?.Invoke(ex);
            }
        }

        private void HandleLobbyJoined(object? sender, LobbyJoinedEventArgs e)
        {
            // Try to load existing value when joining a lobby
            TryLoadExistingValue();
        }

        /// <summary>
        /// Releases all resources used by the <see cref="HostSyncVar{T}"/>.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            UnsubscribeFromChanges();
            OnValueChanged = null;
            OnWriteIgnored = null;
            OnSyncError = null;

            _disposed = true;
        }
    }
}
