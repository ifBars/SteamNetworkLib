using System;
using System.Collections.Generic;
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
    /// A client-owned synchronized variable where each client can set their own value,
    /// and all values are visible to all lobby members via Steam member data.
    /// </summary>
    /// <typeparam name="T">The type of value to synchronize.</typeparam>
    /// <remarks>
    /// <para><strong>Authority Model:</strong> Each client owns and can modify their own value.
    /// All clients can read all other clients' values.</para>
    /// 
    /// <para><strong>Storage:</strong> Uses Steam lobby member data for storage, which is automatically
    /// synchronized to all lobby members by Steam.</para>
    /// 
    /// <para><strong>Use Cases:</strong></para>
    /// <list type="bullet">
    /// <item><description>Ready status for each player</description></item>
    /// <item><description>Player loadouts or preferences</description></item>
    /// <item><description>Per-player scores or statistics</description></item>
    /// <item><description>Player customization options visible to others</description></item>
    /// </list>
    /// 
    /// <example>
    /// <code>
    /// // Create a client-owned sync var
    /// var isReady = client.CreateClientSyncVar("Ready", false);
    /// 
    /// // Subscribe to any client's changes
    /// isReady.OnValueChanged += (playerId, oldVal, newVal) => 
    ///     MelonLogger.Msg($"Player {playerId} ready: {newVal}");
    /// 
    /// // Set my own value
    /// isReady.Value = true;
    /// 
    /// // Read another player's value
    /// bool player2Ready = isReady.GetValue(player2Id);
    /// 
    /// // Get all players' values
    /// var allReadyStates = isReady.GetAllValues();
    /// </code>
    /// </example>
    /// </remarks>
    public class ClientSyncVar<T> : IDisposable
    {
        private readonly SteamNetworkClient _client;
        private readonly string _key;
        private readonly string _fullKey;
        private readonly NetworkSyncOptions _options;
        private readonly ISyncSerializer _serializer;
        private readonly ISyncValidator<T>? _validator;
        private readonly T _defaultValue;
        private T _localValue;
        private readonly Dictionary<ulong, T> _playerValues = new Dictionary<ulong, T>();
        private bool _disposed;
        private bool _isSubscribed;
        private DateTime _lastSyncTime;
        private T? _pendingValue;
        private bool _hasPendingValue;

        /// <summary>
        /// Gets or sets the local player's value.
        /// </summary>
        /// <remarks>
        /// <para>This is a shorthand for getting/setting the value for the local player.</para>
        /// <para>Use <see cref="GetValue(CSteamID)"/> to get other players' values.</para>
        /// </remarks>
        public T Value
        {
            get => _localValue;
            set => SetValue(value);
        }

        /// <summary>
        /// Occurs when any player's value changes.
        /// </summary>
        /// <remarks>
        /// Parameters: (playerId, oldValue, newValue)
        /// </remarks>
        public event Action<CSteamID, T, T>? OnValueChanged;

        /// <summary>
        /// Occurs when the local player's value changes.
        /// </summary>
        /// <remarks>
        /// <para>This is a convenience event that filters <see cref="OnValueChanged"/> 
        /// to only fire for the local player.</para>
        /// <para>Parameters: (oldValue, newValue)</para>
        /// </remarks>
        public event Action<T, T>? OnMyValueChanged;

        /// <summary>
        /// Occurs when a sync operation fails (serialization error, etc.).
        /// </summary>
        public event Action<Exception>? OnSyncError;

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
        /// Initializes a new instance of the <see cref="ClientSyncVar{T}"/> class.
        /// </summary>
        /// <param name="client">The Steam network client.</param>
        /// <param name="key">The unique key for this sync variable.</param>
        /// <param name="defaultValue">The default value when a player hasn't set a value.</param>
        /// <param name="options">Optional configuration options.</param>
        /// <param name="validator">Optional validator for value constraints.</param>
        /// <exception cref="ArgumentNullException">Thrown when client or key is null.</exception>
        /// <exception cref="ArgumentException">Thrown when key is empty or invalid.</exception>
        /// <exception cref="SyncSerializationException">Thrown when the type T cannot be serialized.</exception>
        internal ClientSyncVar(SteamNetworkClient client, string key, T defaultValue, NetworkSyncOptions? options = null, ISyncValidator<T>? validator = null)
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
            _localValue = defaultValue;
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

            // Subscribe to member data changes
            SubscribeToChanges();

            // Try to load existing values if we're in a lobby
            TryLoadExistingValues();
        }

        private void SetValue(T newValue)
        {
            if (!_client.IsInLobby)
            {
                if (_options.WarnOnIgnoredWrites)
                {
                    Console.WriteLine($"[SteamNetworkLib] ClientSyncVar '{_key}': Write ignored - not in a lobby.");
                }
                return;
            }

            // Check if value actually changed
            if (Equals(_localValue, newValue))
            {
                return;
            }

            // Validate the new value
            if (_validator != null && !_validator.IsValid(newValue))
            {
                var errorMsg = _validator.GetErrorMessage(newValue) ?? "Validation failed";
                var fullMsg = $"[SteamNetworkLib] ClientSyncVar '{_key}': {errorMsg}";

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
            var oldValue = _localValue;

            try
            {
                // Serialize and store as member data
                var serialized = _serializer.Serialize(newValue);
                _client.SetMyData(_fullKey, serialized);
                
                // Update local value and cache
                _localValue = newValue;
                _playerValues[_client.LocalPlayerId.m_SteamID] = newValue;
                _lastSyncTime = DateTime.UtcNow;
                _hasPendingValue = false;

                // Fire change events
                OnValueChanged?.Invoke(_client.LocalPlayerId, oldValue, newValue);
                OnMyValueChanged?.Invoke(oldValue, newValue);
            }
            catch (Exception ex)
            {
                OnSyncError?.Invoke(ex);
                
                if (_options.WarnOnIgnoredWrites)
                {
                    Console.WriteLine($"[SteamNetworkLib] ClientSyncVar '{_key}': Sync error - {ex.Message}");
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
        /// Gets the value for a specific player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player.</param>
        /// <returns>The player's value, or the default value if not set.</returns>
        public T GetValue(CSteamID playerId)
        {
            // Check cache first
            if (_playerValues.TryGetValue(playerId.m_SteamID, out var cachedValue))
            {
                return cachedValue;
            }

            // Try to load from Steam
            if (_client.IsInLobby)
            {
                try
                {
                    var serialized = _client.GetPlayerData(playerId, _fullKey);
                    if (!string.IsNullOrEmpty(serialized))
                    {
                        var value = _serializer.Deserialize<T>(serialized);
                        _playerValues[playerId.m_SteamID] = value;
                        return value;
                    }
                }
                catch (Exception ex)
                {
                    OnSyncError?.Invoke(ex);
                }
            }

            return _defaultValue;
        }

        /// <summary>
        /// Gets the values for all players in the lobby.
        /// </summary>
        /// <returns>A dictionary mapping player Steam IDs to their values.</returns>
        public Dictionary<CSteamID, T> GetAllValues()
        {
            var result = new Dictionary<CSteamID, T>();

            if (!_client.IsInLobby)
            {
                return result;
            }

            var members = _client.GetLobbyMembers();
            foreach (var member in members)
            {
                result[member.SteamId] = GetValue(member.SteamId);
            }

            return result;
        }

        /// <summary>
        /// Forces a refresh of all values from Steam member data.
        /// </summary>
        public void Refresh()
        {
            _playerValues.Clear();
            TryLoadExistingValues();
        }

        private void TryLoadExistingValues()
        {
            if (!_client.IsInLobby)
            {
                return;
            }

            try
            {
                var members = _client.GetLobbyMembers();
                foreach (var member in members)
                {
                    var serialized = _client.GetPlayerData(member.SteamId, _fullKey);
                    if (!string.IsNullOrEmpty(serialized))
                    {
                        var value = _serializer.Deserialize<T>(serialized);
                        _playerValues[member.SteamId.m_SteamID] = value;

                        // Update local value if this is us
                        if (member.SteamId == _client.LocalPlayerId)
                        {
                            _localValue = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnSyncError?.Invoke(ex);
            }
        }

        private void SubscribeToChanges()
        {
            if (_isSubscribed) return;

            _client.OnMemberDataChanged += HandleMemberDataChanged;
            _client.OnLobbyJoined += HandleLobbyJoined;
            _client.OnMemberJoined += HandleMemberJoined;
            _client.OnMemberLeft += HandleMemberLeft;
            _isSubscribed = true;
        }

        private void UnsubscribeFromChanges()
        {
            if (!_isSubscribed) return;

            _client.OnMemberDataChanged -= HandleMemberDataChanged;
            _client.OnLobbyJoined -= HandleLobbyJoined;
            _client.OnMemberJoined -= HandleMemberJoined;
            _client.OnMemberLeft -= HandleMemberLeft;
            _isSubscribed = false;
        }

        private void HandleMemberDataChanged(object? sender, MemberDataChangedEventArgs e)
        {
            // Only process changes for our key
            if (e.Key != _fullKey) return;

            try
            {
                var oldValue = _playerValues.TryGetValue(e.MemberId.m_SteamID, out var cached) 
                    ? cached 
                    : _defaultValue;

                T newValue;
                if (string.IsNullOrEmpty(e.NewValue))
                {
                    // Value was removed - reset to default
                    newValue = _defaultValue;
                    _playerValues.Remove(e.MemberId.m_SteamID);
                }
                else
                {
                    newValue = _serializer.Deserialize<T>(e.NewValue);
                    _playerValues[e.MemberId.m_SteamID] = newValue;
                }

                // Update local value if this is us
                if (e.MemberId == _client.LocalPlayerId)
                {
                    _localValue = newValue;
                }

                // Fire events if value changed
                if (!Equals(oldValue, newValue))
                {
                    OnValueChanged?.Invoke(e.MemberId, oldValue, newValue);

                    if (e.MemberId == _client.LocalPlayerId)
                    {
                        OnMyValueChanged?.Invoke(oldValue, newValue);
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
            // Refresh all values when joining a lobby
            _playerValues.Clear();
            TryLoadExistingValues();
        }

        private void HandleMemberJoined(object? sender, MemberJoinedEventArgs e)
        {
            // Try to load the new member's value
            try
            {
                var serialized = _client.GetPlayerData(e.Member.SteamId, _fullKey);
                if (!string.IsNullOrEmpty(serialized))
                {
                    var value = _serializer.Deserialize<T>(serialized);
                    _playerValues[e.Member.SteamId.m_SteamID] = value;
                }
            }
            catch (Exception ex)
            {
                OnSyncError?.Invoke(ex);
            }
        }

        private void HandleMemberLeft(object? sender, MemberLeftEventArgs e)
        {
            // Remove the departed member's cached value
            _playerValues.Remove(e.Member.SteamId.m_SteamID);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="ClientSyncVar{T}"/>.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            UnsubscribeFromChanges();
            OnValueChanged = null;
            OnMyValueChanged = null;
            OnSyncError = null;
            _playerValues.Clear();

            _disposed = true;
        }
    }
}
