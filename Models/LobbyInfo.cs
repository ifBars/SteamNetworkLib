#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Represents information about a Steam lobby including its metadata and current state.
    /// Contains all essential details needed to identify and manage a lobby session.
    /// </summary>
    public class LobbyInfo
    {
        /// <summary>
        /// Gets or sets the unique Steam ID of the lobby.
        /// This is the primary identifier used for all lobby operations.
        /// </summary>
        public CSteamID LobbyId { get; set; }

        /// <summary>
        /// Gets or sets the Steam ID of the lobby owner (host).
        /// The owner has special privileges like changing lobby settings and kicking members.
        /// </summary>
        public CSteamID OwnerId { get; set; }

        /// <summary>
        /// Gets or sets the current number of members in the lobby.
        /// This count includes all connected players including the host.
        /// </summary>
        public int MemberCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of members allowed in the lobby.
        /// This limit is set when the lobby is created and determines capacity.
        /// </summary>
        public int MaxMembers { get; set; }

        /// <summary>
        /// Gets or sets the display name or title of the lobby.
        /// This is an optional human-readable identifier for the lobby.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets when the lobby was created or when it was joined locally.
        /// Used for tracking session duration and ordering lobby lists.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyInfo"/> class.
        /// Sets the creation time to the current local time.
        /// </summary>
        public LobbyInfo()
        {
            CreatedAt = DateTime.Now;
        }
    }
}