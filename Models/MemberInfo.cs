#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Represents information about a lobby member including their identity and status.
    /// Contains all essential details needed to identify and manage a player in a lobby.
    /// </summary>
    public class MemberInfo
    {
        /// <summary>
        /// Gets or sets the unique Steam ID of the member.
        /// This is the primary identifier used for all player-specific operations.
        /// </summary>
        public CSteamID SteamId { get; set; }

        /// <summary>
        /// Gets or sets the member Steam ID as a 64-bit integer.
        /// This is often easier to store, compare, serialize, or log than the runtime-specific Steamworks type.
        /// </summary>
        public ulong SteamId64
        {
            get => SteamId.m_SteamID;
            set => SteamId = new CSteamID(value);
        }

        /// <summary>
        /// Gets the member Steam ID as an invariant decimal string.
        /// </summary>
        public string SteamIdString => SteamId.m_SteamID.ToString();

        /// <summary>
        /// Gets or sets the display name of the member as shown in Steam.
        /// This is the human-readable name that other players will see.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this member is the lobby owner (host).
        /// The owner has special privileges like changing lobby settings and managing members.
        /// </summary>
        public bool IsOwner { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this member represents the local player.
        /// This helps distinguish the local player from other lobby members in the UI.
        /// </summary>
        public bool IsLocalPlayer { get; set; }

        /// <summary>
        /// Gets or sets when this member joined the lobby.
        /// Used for tracking session duration and determining join order.
        /// </summary>
        public DateTime JoinedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberInfo"/> class.
        /// Sets the join time to the current local time.
        /// </summary>
        public MemberInfo()
        {
            JoinedAt = DateTime.Now;
        }
    }
}
