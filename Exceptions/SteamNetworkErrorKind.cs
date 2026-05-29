namespace SteamNetworkLib.Exceptions
{
    /// <summary>
    /// Identifies the broad reason a SteamNetworkLib operation failed.
    /// </summary>
    /// <remarks>
    /// Use this value for diagnostics, retry decisions, and user-facing fallback logic
    /// without parsing exception message text. New values may be added over time.
    /// </remarks>
    public enum SteamNetworkErrorKind
    {
        /// <summary>
        /// No specific failure kind was provided.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Steamworks is unavailable or has not initialized for the current process.
        /// </summary>
        SteamUnavailable,

        /// <summary>
        /// SteamNetworkLib was used before initialization completed.
        /// </summary>
        NotInitialized,

        /// <summary>
        /// The operation requires an active lobby or networking session.
        /// </summary>
        NotInLobby,

        /// <summary>
        /// A Steam ID was missing, zero, nil, or otherwise invalid.
        /// </summary>
        InvalidSteamId,

        /// <summary>
        /// The operation requires host ownership or elevated lobby authority.
        /// </summary>
        PermissionDenied,

        /// <summary>
        /// A packet or payload exceeded the configured or Steamworks packet limit.
        /// </summary>
        PacketTooLarge,

        /// <summary>
        /// A message or payload could not be serialized.
        /// </summary>
        SerializationFailed,

        /// <summary>
        /// A value failed validation before it could be sent or persisted.
        /// </summary>
        ValidationFailed,

        /// <summary>
        /// A packet or serialized message did not match the expected format.
        /// </summary>
        MessageFormatInvalid,

        /// <summary>
        /// A packet's message type did not match the expected message class.
        /// </summary>
        MessageTypeMismatch,

        /// <summary>
        /// A P2P session could not be established or failed while active.
        /// </summary>
        SessionFailed,

        /// <summary>
        /// Steamworks or SteamNetworkLib rejected a lobby data operation.
        /// </summary>
        LobbyDataFailed,

        /// <summary>
        /// Steamworks or SteamNetworkLib rejected a member data operation.
        /// </summary>
        MemberDataFailed
    }
}
