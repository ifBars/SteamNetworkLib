using SteamNetworkLib.Events;
using SteamNetworkLib.Exceptions;
using SteamNetworkLib.Utilities;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Collections.Generic;

namespace SteamNetworkLib.Core
{
    /// <summary>
    /// Manages Steam lobby data (global key-value storage for the lobby).
    /// Provides functionality for getting lobby-wide data that is accessible to all players.
    /// Only the lobby host can set and manage the lobby data.
    /// </summary>
    public class SteamLobbyData : IDisposable
    {
        private readonly SteamLobbyManager _lobbyManager;
        private readonly Dictionary<string, string> _cachedData = new Dictionary<string, string>();
        private bool _disposed = false;

        // Steam callback for lobby data updates
        private Callback<LobbyDataUpdate_t>? _lobbyDataUpdateCallback;

        /// <summary>
        /// Occurs when lobby data is changed by any player in the lobby.
        /// </summary>
        public event EventHandler<LobbyDataChangedEventArgs>? OnLobbyDataChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamLobbyData"/> class.
        /// </summary>
        /// <param name="lobbyManager">The lobby manager instance to use for lobby operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when lobbyManager is null.</exception>
        public SteamLobbyData(SteamLobbyManager lobbyManager)
        {
            _lobbyManager = lobbyManager ?? throw new ArgumentNullException(nameof(lobbyManager));

            // Initialize Steam callbacks
            if (SteamNetworkUtils.IsSteamInitialized())
            {
#if IL2CPP
                _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(new System.Action<LobbyDataUpdate_t>(OnLobbyDataUpdateCallback));
#else
                _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdateCallback);
#endif
            }
        }

        /// <summary>
        /// Sets lobby-wide data that is accessible to all players in the lobby.
        /// </summary>
        /// <param name="key">The data key. Cannot be null, empty, or exceed 255 characters.</param>
        /// <param name="value">The data value to set.</param>
        /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or the operation fails.</exception>
        public void SetData(string key, string value)
        {
            ValidateKey(key);

            if (!_lobbyManager.IsInLobby)
            {
                throw new LobbyException("Cannot set lobby data - not in a lobby");
            }

            var lobbyId = _lobbyManager.CurrentLobby!.LobbyId;
            var oldValue = GetData(key);

            bool success = SteamMatchmaking.SetLobbyData(lobbyId, key, value);
            if (!success)
            {
                throw new LobbyException($"Failed to set lobby data for key: {key}");
            }

            // Update cache
            _cachedData[key] = value;

            // Fire event
            OnLobbyDataChanged?.Invoke(this, new LobbyDataChangedEventArgs(key, oldValue, value, _lobbyManager.LocalPlayerID));
        }

        /// <summary>
        /// Gets lobby-wide data by key.
        /// </summary>
        /// <param name="key">The data key to retrieve.</param>
        /// <returns>The data value if found, or null if not found or not in a lobby.</returns>
        /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
        public string? GetData(string key)
        {
            ValidateKey(key);

            if (!_lobbyManager.IsInLobby)
            {
                return null;
            }

            var lobbyId = _lobbyManager.CurrentLobby!.LobbyId;
            var value = SteamMatchmaking.GetLobbyData(lobbyId, key);

            // Update cache
            if (!string.IsNullOrEmpty(value))
            {
                _cachedData[key] = value;
                return value;
            }

            // Return cached value if Steam returned empty
            return _cachedData.TryGetValue(key, out var cached) ? cached : null;
        }

        /// <summary>
        /// Checks whether lobby data exists for the specified key.
        /// </summary>
        /// <param name="key">The data key to check.</param>
        /// <returns>True if the key exists and has a non-empty value, false otherwise.</returns>
        public bool HasData(string key)
        {
            return !string.IsNullOrEmpty(GetData(key));
        }

        /// <summary>
        /// Removes lobby data for the specified key.
        /// </summary>
        /// <param name="key">The data key to remove.</param>
        /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or the operation fails.</exception>
        public void RemoveData(string key)
        {
            ValidateKey(key);

            if (!_lobbyManager.IsInLobby)
            {
                throw new LobbyException("Cannot remove lobby data - not in a lobby");
            }

            var oldValue = GetData(key);
            if (oldValue == null) return; // Nothing to remove

            var lobbyId = _lobbyManager.CurrentLobby!.LobbyId;
            bool success = SteamMatchmaking.DeleteLobbyData(lobbyId, key);

            if (!success)
            {
                throw new LobbyException($"Failed to remove lobby data for key: {key}");
            }

            // Update cache
            _cachedData.Remove(key);

            // Fire event
            OnLobbyDataChanged?.Invoke(this, new LobbyDataChangedEventArgs(key, oldValue, null, _lobbyManager.LocalPlayerID));
        }

        /// <summary>
        /// Gets all lobby data as a dictionary.
        /// </summary>
        /// <returns>A dictionary containing all key-value pairs of lobby data, or an empty dictionary if not in a lobby.</returns>
        public Dictionary<string, string> GetAllData()
        {
            var result = new Dictionary<string, string>();

            if (!_lobbyManager.IsInLobby)
            {
                return result;
            }

            var lobbyId = _lobbyManager.CurrentLobby!.LobbyId;
            var dataCount = SteamMatchmaking.GetLobbyDataCount(lobbyId);

            for (int i = 0; i < dataCount; i++)
            {
                string key = "";
                string value = "";

                if (SteamMatchmaking.GetLobbyDataByIndex(lobbyId, i, out key, 256, out value, 256))
                {
                    if (!string.IsNullOrEmpty(key))
                    {
                        result[key] = value;
                        _cachedData[key] = value; // Update cache
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Clears all lobby data. Only the lobby host can perform this operation.
        /// </summary>
        /// <exception cref="LobbyException">Thrown when not in a lobby or not the lobby host.</exception>
        public void ClearAllData()
        {
            if (!_lobbyManager.IsInLobby)
            {
                throw new LobbyException("Cannot clear lobby data - not in a lobby");
            }

            if (!_lobbyManager.IsHost)
            {
                throw new LobbyException("Only the lobby host can clear all lobby data");
            }

            var allData = GetAllData();
            foreach (var key in allData.Keys)
            {
                RemoveData(key);
            }
        }

        /// <summary>
        /// Sets multiple lobby data values in a batch operation.
        /// </summary>
        /// <param name="data">A dictionary containing key-value pairs to set. Null or empty dictionaries are ignored.</param>
        /// <exception cref="LobbyException">Thrown when not in a lobby or any individual set operation fails.</exception>
        public void SetDataBatch(Dictionary<string, string> data)
        {
            if (data == null || data.Count == 0) return;

            if (!_lobbyManager.IsInLobby)
            {
                throw new LobbyException("Cannot set lobby data - not in a lobby");
            }

            // Set all data in batch
            foreach (var kvp in data)
            {
                try
                {
                    SetData(kvp.Key, kvp.Value);
                }
                catch (Exception ex)
                {
                    throw new LobbyException($"Failed to set data for key '{kvp.Key}': {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Gets the number of data entries in the lobby.
        /// </summary>
        /// <returns>The count of lobby data entries, or 0 if not in a lobby.</returns>
        public int GetDataCount()
        {
            if (!_lobbyManager.IsInLobby)
            {
                return 0;
            }

            return SteamMatchmaking.GetLobbyDataCount(_lobbyManager.CurrentLobby!.LobbyId);
        }

        /// <summary>
        /// Gets a list of all data keys in the lobby.
        /// </summary>
        /// <returns>A list of all data keys, or an empty list if not in a lobby.</returns>
        public List<string> GetDataKeys()
        {
            var keys = new List<string>();

            if (!_lobbyManager.IsInLobby)
            {
                return keys;
            }

            var lobbyId = _lobbyManager.CurrentLobby!.LobbyId;
            var dataCount = SteamMatchmaking.GetLobbyDataCount(lobbyId);

            for (int i = 0; i < dataCount; i++)
            {
                string key = "";
                string value = "";

                if (SteamMatchmaking.GetLobbyDataByIndex(lobbyId, i, out key, 256, out value, 256))
                {
                    if (!string.IsNullOrEmpty(key))
                    {
                        keys.Add(key);
                    }
                }
            }

            return keys;
        }

        /// <summary>
        /// Refreshes the lobby data cache by reloading all data from Steam servers.
        /// </summary>
        public void RefreshData()
        {
            if (!_lobbyManager.IsInLobby) return;

            // Clear cache and reload from Steam
            _cachedData.Clear();
            GetAllData(); // This will repopulate the cache
        }

        /// <summary>
        /// Helper method to validate data keys.
        /// </summary>
        private void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Lobby data key cannot be null or empty");
            }

            if (key.Length > 255)
            {
                throw new ArgumentException("Lobby data key cannot exceed 255 characters");
            }

            // Steam has some reserved key prefixes
            if (key.StartsWith("__steam_"))
            {
                throw new ArgumentException("Lobby data key cannot start with '__steam_' (reserved by Steam)");
            }
        }

        /// <summary>
        /// Steam callback for lobby data updates.
        /// </summary>
        private void OnLobbyDataUpdateCallback(LobbyDataUpdate_t result)
        {
            try
            {
                // Only process updates for our current lobby
                if (!_lobbyManager.IsInLobby ||
                    _lobbyManager.CurrentLobby!.LobbyId.m_SteamID != result.m_ulSteamIDLobby)
                {
                    return;
                }

                // Check if it's a member data update (different callback type)
                if (result.m_bSuccess == 0)
                {
                    return; // Data update failed
                }

                // Refresh our cache since data was updated
                var previousData = new Dictionary<string, string>(_cachedData);
                RefreshData();

                // Find what changed and fire events
                var currentData = _cachedData;
                var changedBy = new CSteamID(result.m_ulSteamIDMember);

                // Check for new/changed keys
                foreach (var kvp in currentData)
                {
                    if (!previousData.TryGetValue(kvp.Key, out var oldValue) || oldValue != kvp.Value)
                    {
                        OnLobbyDataChanged?.Invoke(this, new LobbyDataChangedEventArgs(kvp.Key, oldValue, kvp.Value, changedBy));
                    }
                }

                // Check for removed keys
                foreach (var kvp in previousData)
                {
                    if (!currentData.ContainsKey(kvp.Key))
                    {
                        OnLobbyDataChanged?.Invoke(this, new LobbyDataChangedEventArgs(kvp.Key, kvp.Value, null, changedBy));
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Console.WriteLine($"Error in lobby data update callback: {ex.Message}");
            }
        }

        /// <summary>
        /// Releases all resources used by the SteamLobbyData.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _lobbyDataUpdateCallback?.Dispose();
                _cachedData.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing SteamLobbyData: {ex.Message}");
            }

            _disposed = true;
        }
    }
}