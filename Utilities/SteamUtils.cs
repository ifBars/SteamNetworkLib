
using System;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif

namespace SteamNetworkLib.Utilities
{
    /// <summary>
    /// Utility methods for Steam networking operations.
    /// </summary>
    public static class SteamNetworkUtils
    {
        /// <summary>
        /// Checks if Steam is initialized and ready for networking.
        /// </summary>
        /// <returns>True if Steam is initialized.</returns>
        public static bool IsSteamInitialized()
        {
            try
            {
                return SteamAPI.IsSteamRunning();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the local Steam user's display name.
        /// </summary>
        /// <returns>Display name of the local user.</returns>
        public static string GetLocalPlayerName()
        {
            if (!IsSteamInitialized()) return "Unknown Player";
            return SteamFriends.GetPersonaName();
        }

        /// <summary>
        /// Gets the display name for a specific Steam user.
        /// </summary>
        /// <param name="steamId">Steam ID of the user.</param>
        /// <returns>Display name of the user.</returns>
        public static string GetPlayerName(CSteamID steamId)
        {
            if (!IsSteamInitialized()) return "Unknown Player";
            return SteamFriends.GetFriendPersonaName(steamId);
        }

        /// <summary>
        /// Validates that a Steam ID is valid and not nil.
        /// Compatible with both real Steam and Goldberg Steam Emu.
        /// </summary>
        /// <param name="steamId">Steam ID to validate.</param>
        /// <returns>True if the Steam ID is valid.</returns>
        public static bool IsValidSteamID(CSteamID steamId)
        {
            // Basic check: not nil and not zero
            if (steamId == CSteamID.Nil || steamId.m_SteamID == 0)
                return false;

            // For Goldberg Steam Emu compatibility, we need to be more permissive
            // Goldberg emu generates IDs that may not pass Steam's official IsValid() check
            try
            {
                // First try the official validation
                if (steamId.IsValid())
                {
                    return true;
                }
                
                // If official validation fails, check if this might be a Goldberg emu ID
                // Goldberg typically uses IDs in certain ranges that are "invalid" by Steam standards
                // but perfectly functional for local testing
                
                // Allow any non-zero ID that's not the official nil value
                // This is more permissive but necessary for Goldberg compatibility
                bool isGoldbergCompatible = steamId.m_SteamID != 0 && steamId.m_SteamID != 1;
                
                return isGoldbergCompatible;
            }
            catch (Exception ex)
            {
                // If IsValid() throws (shouldn't happen but just in case), fall back to basic check
                return steamId.m_SteamID != 0 && steamId.m_SteamID != 1;
            }
        }

        /// <summary>
        /// Converts a string to a Steam ID safely.
        /// </summary>
        /// <param name="steamIdString">String representation of Steam ID.</param>
        /// <returns>Steam ID if valid, null otherwise.</returns>
        public static CSteamID? ParseSteamID(string steamIdString)
        {
            if (string.IsNullOrEmpty(steamIdString)) return null;

            if (ulong.TryParse(steamIdString, out ulong steamId))
            {
                var id = new CSteamID(steamId);
                return IsValidSteamID(id) ? id : null;
            }

            return null;
        }

        /// <summary>
        /// Checks if a specific user is a friend of the local player.
        /// </summary>
        /// <param name="steamId">Steam ID to check.</param>
        /// <returns>True if the user is a friend.</returns>
        public static bool IsFriend(CSteamID steamId)
        {
            if (!IsSteamInitialized() || !IsValidSteamID(steamId)) return false;

            var relationship = SteamFriends.GetFriendRelationship(steamId);
            return relationship == EFriendRelationship.k_EFriendRelationshipFriend;
        }

        /// <summary>
        /// Formats a lobby type for display.
        /// </summary>
        /// <param name="lobbyType">The lobby type.</param>
        /// <returns>Human-readable lobby type string.</returns>
        public static string FormatLobbyType(ELobbyType lobbyType)
        {
            return lobbyType switch
            {
                ELobbyType.k_ELobbyTypePrivate => "Private",
                ELobbyType.k_ELobbyTypeFriendsOnly => "Friends Only",
                ELobbyType.k_ELobbyTypePublic => "Public",
                ELobbyType.k_ELobbyTypeInvisible => "Invisible",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Formats a Steam result for display.
        /// </summary>
        /// <param name="result">The Steam result.</param>
        /// <returns>Human-readable result string.</returns>
        public static string FormatSteamResult(EResult result)
        {
            return result switch
            {
                EResult.k_EResultOK => "Success",
                EResult.k_EResultFail => "Generic failure",
                EResult.k_EResultNoConnection => "No connection to Steam",
                EResult.k_EResultInvalidPassword => "Invalid password",
                EResult.k_EResultLoggedInElsewhere => "Logged in elsewhere",
                EResult.k_EResultInvalidProtocolVer => "Invalid protocol version",
                EResult.k_EResultInvalidParam => "Invalid parameter",
                EResult.k_EResultFileNotFound => "File not found",
                EResult.k_EResultBusy => "Service busy",
                EResult.k_EResultInvalidState => "Invalid state",
                EResult.k_EResultInvalidName => "Invalid name",
                EResult.k_EResultInvalidEmail => "Invalid email",
                EResult.k_EResultDuplicateName => "Duplicate name",
                EResult.k_EResultAccessDenied => "Access denied",
                EResult.k_EResultTimeout => "Timeout",
                EResult.k_EResultBanned => "Banned",
                EResult.k_EResultAccountNotFound => "Account not found",
                EResult.k_EResultInvalidSteamID => "Invalid Steam ID",
                EResult.k_EResultServiceUnavailable => "Service unavailable",
                EResult.k_EResultNotLoggedOn => "Not logged on",
                EResult.k_EResultPending => "Request pending",
                EResult.k_EResultEncryptionFailure => "Encryption failure",
                EResult.k_EResultInsufficientPrivilege => "Insufficient privilege",
                EResult.k_EResultLimitExceeded => "Limit exceeded",
                EResult.k_EResultRevoked => "Access revoked",
                EResult.k_EResultExpired => "License expired",
                EResult.k_EResultAlreadyRedeemed => "Already redeemed",
                EResult.k_EResultDuplicateRequest => "Duplicate request",
                EResult.k_EResultAlreadyOwned => "Already owned",
                EResult.k_EResultIPNotFound => "IP not found",
                EResult.k_EResultPersistFailed => "Persist failed",
                EResult.k_EResultLockingFailed => "Locking failed",
                EResult.k_EResultLogonSessionReplaced => "Logon session replaced",
                EResult.k_EResultConnectFailed => "Connect failed",
                EResult.k_EResultHandshakeFailed => "Handshake failed",
                EResult.k_EResultIOFailure => "IO failure",
                EResult.k_EResultRemoteDisconnect => "Remote disconnect",
                EResult.k_EResultShoppingCartNotFound => "Shopping cart not found",
                EResult.k_EResultBlocked => "Blocked",
                EResult.k_EResultIgnored => "Ignored",
                EResult.k_EResultNoMatch => "No match",
                EResult.k_EResultAccountDisabled => "Account disabled",
                EResult.k_EResultServiceReadOnly => "Service read only",
                EResult.k_EResultAccountNotFeatured => "Account not featured",
                EResult.k_EResultAdministratorOK => "Administrator OK",
                EResult.k_EResultContentVersion => "Content version",
                EResult.k_EResultTryAnotherCM => "Try another CM",
                EResult.k_EResultPasswordRequiredToKickSession => "Password required to kick session",
                EResult.k_EResultAlreadyLoggedInElsewhere => "Already logged in elsewhere",
                EResult.k_EResultSuspended => "Suspended",
                EResult.k_EResultCancelled => "Cancelled",
                EResult.k_EResultDataCorruption => "Data corruption",
                EResult.k_EResultDiskFull => "Disk full",
                EResult.k_EResultRemoteCallFailed => "Remote call failed",
                _ => $"Unknown result: {result}"
            };
        }
    }
}