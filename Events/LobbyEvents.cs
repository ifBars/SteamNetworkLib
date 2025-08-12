using SteamNetworkLib.Models;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Collections.Generic;

namespace SteamNetworkLib.Events
{
    /// <summary>
    /// Provides data for the lobby joined event.
    /// Contains information about the lobby that was joined and the result of the join operation.
    /// </summary>
    public class LobbyJoinedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the lobby information for the joined lobby.
        /// </summary>
        public LobbyInfo Lobby { get; }

        /// <summary>
        /// Gets the result of the lobby join operation.
        /// </summary>
        public EResult Result { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyJoinedEventArgs"/> class.
        /// </summary>
        /// <param name="lobby">The lobby information for the joined lobby.</param>
        /// <param name="result">The result of the lobby join operation.</param>
        public LobbyJoinedEventArgs(LobbyInfo lobby, EResult result = EResult.k_EResultOK)
        {
            Lobby = lobby;
            Result = result;
        }
    }

    /// <summary>
    /// Provides data for the lobby created event.
    /// Contains information about the newly created lobby and the result of the creation operation.
    /// </summary>
    public class LobbyCreatedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the lobby information for the created lobby.
        /// </summary>
        public LobbyInfo Lobby { get; }

        /// <summary>
        /// Gets the result of the lobby creation operation.
        /// </summary>
        public EResult Result { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyCreatedEventArgs"/> class.
        /// </summary>
        /// <param name="lobby">The lobby information for the created lobby.</param>
        /// <param name="result">The result of the lobby creation operation.</param>
        public LobbyCreatedEventArgs(LobbyInfo lobby, EResult result = EResult.k_EResultOK)
        {
            Lobby = lobby;
            Result = result;
        }
    }

    /// <summary>
    /// Provides data for the lobby left event.
    /// Contains information about the lobby that was left and the reason for leaving.
    /// </summary>
    public class LobbyLeftEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the Steam ID of the lobby that was left.
        /// </summary>
        public CSteamID LobbyId { get; }

        /// <summary>
        /// Gets the reason for leaving the lobby, or an empty string if no specific reason was provided.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyLeftEventArgs"/> class.
        /// </summary>
        /// <param name="lobbyId">The Steam ID of the lobby that was left.</param>
        /// <param name="reason">The reason for leaving the lobby.</param>
        public LobbyLeftEventArgs(CSteamID lobbyId, string reason = "")
        {
            LobbyId = lobbyId;
            Reason = reason;
        }
    }

    /// <summary>
    /// Provides data for the member joined event.
    /// Contains information about the member who joined the lobby.
    /// </summary>
    public class MemberJoinedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the information about the member who joined the lobby.
        /// </summary>
        public MemberInfo Member { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberJoinedEventArgs"/> class.
        /// </summary>
        /// <param name="member">The information about the member who joined the lobby.</param>
        public MemberJoinedEventArgs(MemberInfo member)
        {
            Member = member;
        }
    }

    /// <summary>
    /// Provides data for the member left event.
    /// Contains information about the member who left the lobby and the reason for leaving.
    /// </summary>
    public class MemberLeftEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the information about the member who left the lobby.
        /// </summary>
        public MemberInfo Member { get; }

        /// <summary>
        /// Gets the reason for leaving the lobby, or an empty string if no specific reason was provided.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberLeftEventArgs"/> class.
        /// </summary>
        /// <param name="member">The information about the member who left the lobby.</param>
        /// <param name="reason">The reason for leaving the lobby.</param>
        public MemberLeftEventArgs(MemberInfo member, string reason = "")
        {
            Member = member;
            Reason = reason;
        }
    }

    /// <summary>
    /// Provides data for the lobby data changed event.
    /// Contains information about what lobby data was changed, including old and new values.
    /// </summary>
    public class LobbyDataChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the key of the lobby data that was changed.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the previous value of the lobby data, or null if it was newly set.
        /// </summary>
        public string? OldValue { get; }

        /// <summary>
        /// Gets the new value of the lobby data, or null if it was removed.
        /// </summary>
        public string? NewValue { get; }

        /// <summary>
        /// Gets the Steam ID of the player who made the change.
        /// </summary>
        public CSteamID ChangedBy { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyDataChangedEventArgs"/> class.
        /// </summary>
        /// <param name="key">The key of the lobby data that was changed.</param>
        /// <param name="oldValue">The previous value of the lobby data.</param>
        /// <param name="newValue">The new value of the lobby data.</param>
        /// <param name="changedBy">The Steam ID of the player who made the change.</param>
        public LobbyDataChangedEventArgs(string key, string? oldValue, string? newValue, CSteamID changedBy)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
            ChangedBy = changedBy;
        }
    }

    /// <summary>
    /// Provides data for the member data changed event.
    /// Contains information about what member data was changed, including old and new values.
    /// </summary>
    public class MemberDataChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the Steam ID of the member whose data was changed.
        /// </summary>
        public CSteamID MemberId { get; }

        /// <summary>
        /// Gets the key of the member data that was changed.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the previous value of the member data, or null if it was newly set.
        /// </summary>
        public string? OldValue { get; }

        /// <summary>
        /// Gets the new value of the member data, or null if it was removed.
        /// </summary>
        public string? NewValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberDataChangedEventArgs"/> class.
        /// </summary>
        /// <param name="memberId">The Steam ID of the member whose data was changed.</param>
        /// <param name="key">The key of the member data that was changed.</param>
        /// <param name="oldValue">The previous value of the member data.</param>
        /// <param name="newValue">The new value of the member data.</param>
        public MemberDataChangedEventArgs(CSteamID memberId, string key, string? oldValue, string? newValue)
        {
            MemberId = memberId;
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    /// <summary>
    /// Event arguments for SteamNetworkLib version mismatches between players.
    /// </summary>
    public class VersionMismatchEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the local player's SteamNetworkLib version.
        /// </summary>
        public string LocalVersion { get; }

        /// <summary>
        /// Gets a dictionary mapping player Steam IDs to their SteamNetworkLib versions.
        /// </summary>
        public Dictionary<CSteamID, string> PlayerVersions { get; }

        /// <summary>
        /// Gets a list of players with incompatible versions.
        /// </summary>
        public List<CSteamID> IncompatiblePlayers { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionMismatchEventArgs"/> class.
        /// </summary>
        /// <param name="localVersion">The local player's SteamNetworkLib version.</param>
        /// <param name="playerVersions">Dictionary mapping player Steam IDs to their versions.</param>
        /// <param name="incompatiblePlayers">List of players with incompatible versions.</param>
        public VersionMismatchEventArgs(string localVersion, Dictionary<CSteamID, string> playerVersions, List<CSteamID> incompatiblePlayers)
        {
            LocalVersion = localVersion;
            PlayerVersions = playerVersions;
            IncompatiblePlayers = incompatiblePlayers;
        }
    }
}