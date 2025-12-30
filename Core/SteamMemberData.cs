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
using System.Linq;

namespace SteamNetworkLib.Core
{
    /// <summary>
    /// Manages Steam lobby member data (per-player key-value storage).
    /// Provides functionality for setting, getting, and managing player-specific data that is visible to all lobby members.
    /// </summary>
    public class SteamMemberData : IDisposable
    {
        private readonly SteamLobbyManager _lobbyManager;
        private readonly Dictionary<(ulong PlayerId, string Key), string> _cachedData = new Dictionary<(ulong, string), string>();
        private bool _disposed = false;

        // Steam callback for lobby data updates (covers member data too)
        private Callback<LobbyDataUpdate_t>? _lobbyDataUpdateCallback;

        /// <summary>
        /// Occurs when member data is changed for any player in the lobby.
        /// </summary>
        public event EventHandler<MemberDataChangedEventArgs>? OnMemberDataChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamMemberData"/> class.
        /// </summary>
        /// <param name="lobbyManager">The lobby manager instance to use for lobby operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when lobbyManager is null.</exception>
        public SteamMemberData(SteamLobbyManager? lobbyManager)
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
        /// Sets data for the local player.
        /// </summary>
        /// <param name="key">The data key. Cannot be null, empty, or exceed 255 characters.</param>
        /// <param name="value">The data value to set.</param>
        /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public void SetMemberData(string key, string value)
        {
            SetMemberData(_lobbyManager.LocalPlayerID, key, value);
        }

        /// <summary>
        /// Sets data for a specific player. Only the local player can set their own data.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player. Must be the local player.</param>
        /// <param name="key">The data key. Cannot be null, empty, or exceed 255 characters.</param>
        /// <param name="value">The data value to set.</param>
        /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby or trying to set data for another player.</exception>
        public void SetMemberData(CSteamID playerId, string key, string value)
        {
            ValidateKey(key);

            if (!_lobbyManager.IsInLobby)
            {
                throw new LobbyException("Cannot set member data - not in a lobby");
            }

            // Only allow setting data for local player
            if (playerId != _lobbyManager.LocalPlayerID)
            {
                throw new LobbyException("Cannot set member data for other players - only for local player");
            }

            var lobbyId = _lobbyManager.CurrentLobby!.LobbyId;
            var oldValue = GetMemberData(playerId, key);

            SteamMatchmaking.SetLobbyMemberData(lobbyId, key, value);

            // Update cache
            _cachedData[(playerId.m_SteamID, key)] = value;

            // Fire event
            OnMemberDataChanged?.Invoke(this, new MemberDataChangedEventArgs(playerId, key, oldValue, value));
        }

        /// <summary>
        /// Gets data for the local player.
        /// </summary>
        /// <param name="key">The data key to retrieve.</param>
        /// <returns>The data value if found, or null if not found or not in a lobby.</returns>
        public string? GetMemberData(string key)
        {
            return GetMemberData(_lobbyManager.LocalPlayerID, key);
        }

        /// <summary>
        /// Gets data for a specific player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player whose data to retrieve.</param>
        /// <param name="key">The data key to retrieve.</param>
        /// <returns>The data value if found, or null if not found or not in a lobby.</returns>
        /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
        public string? GetMemberData(CSteamID playerId, string key)
        {
            ValidateKey(key);

            if (!_lobbyManager.IsInLobby)
            {
                return null;
            }

            var lobbyId = _lobbyManager.CurrentLobby!.LobbyId;
            var value = SteamMatchmaking.GetLobbyMemberData(lobbyId, playerId, key);

            // Update cache
            if (!string.IsNullOrEmpty(value))
            {
                _cachedData[(playerId.m_SteamID, key)] = value;
                return value;
            }

            // Return cached value if Steam returned empty
            return _cachedData.TryGetValue((playerId.m_SteamID, key), out var cached) ? cached : null;
        }

        /// <summary>
        /// Checks whether the local player has data for the specified key.
        /// </summary>
        /// <param name="key">The data key to check.</param>
        /// <returns>True if the key exists and has a non-empty value, false otherwise.</returns>
        public bool HasMemberData(string key)
        {
            return HasMemberData(_lobbyManager.LocalPlayerID, key);
        }

        /// <summary>
        /// Checks whether a specific player has data for the specified key.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player to check.</param>
        /// <param name="key">The data key to check.</param>
        /// <returns>True if the key exists and has a non-empty value, false otherwise.</returns>
        public bool HasMemberData(CSteamID playerId, string key)
        {
            return !string.IsNullOrEmpty(GetMemberData(playerId, key));
        }

        /// <summary>
        /// Removes data for the local player.
        /// </summary>
        /// <param name="key">The data key to remove.</param>
        /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
        /// <exception cref="LobbyException">Thrown when not in a lobby.</exception>
        public void RemoveMemberData(string key)
        {
            ValidateKey(key);

            if (!_lobbyManager.IsInLobby)
            {
                throw new LobbyException("Cannot remove member data - not in a lobby");
            }

            var playerId = _lobbyManager.LocalPlayerID;
            var lobbyId = _lobbyManager.CurrentLobby!.LobbyId;
            var oldValue = GetMemberData(playerId, key);

            if (oldValue == null) return; // Nothing to remove

            // Steam doesn't have a direct "delete member data" method, so we set it to empty
            SteamMatchmaking.SetLobbyMemberData(lobbyId, key, "");

            // Update cache
            _cachedData.Remove((playerId.m_SteamID, key));

            // Fire event
            OnMemberDataChanged?.Invoke(this, new MemberDataChangedEventArgs(playerId, key, oldValue, null));
        }

        /// <summary>
        /// Gets all data for a specific player.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player whose data to retrieve.</param>
        /// <returns>A dictionary containing all key-value pairs for the specified player, or an empty dictionary if not in a lobby.</returns>
        public Dictionary<string, string> GetAllMemberData(CSteamID playerId)
        {
            var result = new Dictionary<string, string>();

            if (!_lobbyManager.IsInLobby)
            {
                return result;
            }

            var lobbyId = _lobbyManager.CurrentLobby!.LobbyId;

            // Steam doesn't provide a direct way to enumerate all member data keys,
            // so we need to rely on known keys or our cache
            var knownKeys = _cachedData.Keys
                .Where(k => k.PlayerId == playerId.m_SteamID)
                .Select(k => k.Key)
                .Distinct()
                .ToList();

            foreach (var key in knownKeys)
            {
                var value = GetMemberData(playerId, key);
                if (!string.IsNullOrEmpty(value))
                {
                    result[key] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the same data key for all players in the lobby.
        /// </summary>
        /// <param name="key">The data key to retrieve for all players.</param>
        /// <returns>A dictionary mapping player Steam IDs to their data values for the specified key.</returns>
        /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
        public Dictionary<CSteamID, string> GetMemberDataForAllPlayers(string key)
        {
            ValidateKey(key);

            var result = new Dictionary<CSteamID, string>();

            if (!_lobbyManager.IsInLobby)
            {
                return result;
            }

            var members = _lobbyManager.GetLobbyMembers();
            foreach (var member in members)
            {
                var value = GetMemberData(member.SteamId, key);
                if (!string.IsNullOrEmpty(value))
                {
                    result[member.SteamId] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Sets multiple data values for the local player in a batch operation.
        /// </summary>
        /// <param name="data">A dictionary containing key-value pairs to set. Null or empty dictionaries are ignored.</param>
        /// <exception cref="LobbyException">Thrown when not in a lobby or any individual set operation fails.</exception>
        public void SetMemberDataBatch(Dictionary<string, string> data)
        {
            if (data == null || data.Count == 0) return;

            if (!_lobbyManager.IsInLobby)
            {
                throw new LobbyException("Cannot set member data - not in a lobby");
            }

            // Set all data in batch for local player
            foreach (var kvp in data)
            {
                try
                {
                    SetMemberData(kvp.Key, kvp.Value);
                }
                catch (Exception ex)
                {
                    throw new LobbyException($"Failed to set member data for key '{kvp.Key}': {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Gets a list of players who have data for the specified key.
        /// </summary>
        /// <param name="key">The data key to check.</param>
        /// <returns>A list of Steam IDs for players who have data for the specified key.</returns>
        /// <exception cref="ArgumentException">Thrown when the key is invalid.</exception>
        public List<CSteamID> GetPlayersWithData(string key)
        {
            ValidateKey(key);

            var result = new List<CSteamID>();

            if (!_lobbyManager.IsInLobby)
            {
                return result;
            }

            var members = _lobbyManager.GetLobbyMembers();
            foreach (var member in members)
            {
                if (HasMemberData(member.SteamId, key))
                {
                    result.Add(member.SteamId);
                }
            }

            return result;
        }

        /// <summary>
        /// Refreshes the member data cache for a specific player by reloading from Steam servers.
        /// </summary>
        /// <param name="playerId">The Steam ID of the player whose data to refresh.</param>
        public void RefreshMemberData(CSteamID playerId)
        {
            if (!_lobbyManager.IsInLobby) return;

            // Clear cache for this player and trigger reloads by calling GetMemberData
            var keysToRefresh = _cachedData.Keys
                .Where(k => k.PlayerId == playerId.m_SteamID)
                .Select(k => k.Key)
                .ToList();

            foreach (var key in keysToRefresh)
            {
                _cachedData.Remove((playerId.m_SteamID, key));
                GetMemberData(playerId, key); // This will reload from Steam and update cache
            }
        }

        /// <summary>
        /// Refreshes the member data cache for all players by reloading from Steam servers.
        /// </summary>
        public void RefreshAllMemberData()
        {
            if (!_lobbyManager.IsInLobby) return;

            var members = _lobbyManager.GetLobbyMembers();
            foreach (var member in members)
            {
                RefreshMemberData(member.SteamId);
            }
        }

        /// <summary>
        /// Gets a summary of all member data across all players in the lobby.
        /// </summary>
        /// <returns>A dictionary mapping player Steam IDs to their complete data dictionaries. Useful for debugging and overview purposes.</returns>
        public Dictionary<CSteamID, Dictionary<string, string>> GetAllMemberDataSummary()
        {
            var result = new Dictionary<CSteamID, Dictionary<string, string>>();

            if (!_lobbyManager.IsInLobby)
            {
                return result;
            }

            var members = _lobbyManager.GetLobbyMembers();
            foreach (var member in members)
            {
                result[member.SteamId] = GetAllMemberData(member.SteamId);
            }

            return result;
        }

        /// <summary>
        /// Helper method to validate data keys.
        /// </summary>
        private void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Member data key cannot be null or empty");
            }

            if (key.Length > 255)
            {
                throw new ArgumentException("Member data key cannot exceed 255 characters");
            }

            // Steam has some reserved key prefixes
            if (key.StartsWith("__steam_"))
            {
                throw new ArgumentException("Member data key cannot start with '__steam_' (reserved by Steam)");
            }
        }

        /// <summary>
        /// Steam callback for lobby data updates (includes member data).
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

                if (result.m_bSuccess == 0)
                {
                    return; // Data update failed
                }

                var memberId = new CSteamID(result.m_ulSteamIDMember);

                // If member ID is not nil, this is likely a member data update
                if (memberId != CSteamID.Nil && SteamNetworkUtils.IsValidSteamID(memberId))
                {
                    // Refresh this member's data
                    RefreshMemberData(memberId);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                Console.WriteLine($"Error in lobby data update callback for member data: {ex.Message}");
            }
        }

        /// <summary>
        /// Releases all resources used by the SteamMemberData.
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
                Console.WriteLine($"Error disposing SteamMemberData: {ex.Message}");
            }

            _disposed = true;
        }
    }
}