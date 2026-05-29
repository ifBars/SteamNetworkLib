#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;

namespace SteamNetworkLib.Exceptions
{
    /// <summary>
    /// Exception thrown when lobby-specific operations fail.
    /// Provides additional context about Steam lobby operations including result codes and lobby IDs.
    /// </summary>
    public class LobbyException : SteamNetworkException
    {
        /// <summary>
        /// Gets the Steam result code associated with the lobby operation, if available.
        /// </summary>
        public EResult? SteamResult { get; }

        /// <summary>
        /// Gets the Steam ID of the lobby associated with the operation, if available.
        /// </summary>
        public CSteamID? LobbyId { get; }

        /// <summary>
        /// Gets the lobby Steam ID as a 64-bit integer, or 0 when no lobby ID is available.
        /// </summary>
        public ulong LobbyId64 => LobbyId?.m_SteamID ?? 0UL;

        /// <summary>
        /// Gets the Steam ID of the member associated with the operation, if available.
        /// </summary>
        public CSteamID? MemberId { get; }

        /// <summary>
        /// Gets the member Steam ID as a 64-bit integer, or 0 when no member ID is available.
        /// </summary>
        public ulong MemberId64 => MemberId?.m_SteamID ?? 0UL;

        /// <summary>
        /// Gets the lobby or member data key associated with the operation, if available.
        /// </summary>
        public string? DataKey { get; }

        /// <summary>
        /// Gets a value indicating whether the failed operation requires lobby host authority.
        /// </summary>
        public bool RequiresHost { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyException"/> class.
        /// </summary>
        public LobbyException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public LobbyException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public LobbyException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyException"/> class with a specified error message and Steam result code.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="steamResult">The Steam result code that indicates the specific failure reason.</param>
        public LobbyException(string message, EResult steamResult) : base(message)
        {
            SteamResult = steamResult;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyException"/> class with a specified error message and lobby ID.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="lobbyId">The Steam ID of the lobby associated with the operation.</param>
        public LobbyException(string message, CSteamID lobbyId) : base(message)
        {
            LobbyId = lobbyId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyException"/> class with a specified error message, Steam result code, and lobby ID.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="steamResult">The Steam result code that indicates the specific failure reason.</param>
        /// <param name="lobbyId">The Steam ID of the lobby associated with the operation.</param>
        public LobbyException(string message, EResult steamResult, CSteamID lobbyId) : base(message)
        {
            SteamResult = steamResult;
            LobbyId = lobbyId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyException"/> class with structured diagnostic details.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorKind">The broad reason the operation failed.</param>
        /// <param name="operation">The API operation or lifecycle step that failed.</param>
        /// <param name="lobbyId">The Steam ID of the lobby associated with the operation, if available.</param>
        /// <param name="memberId">The Steam ID of the member associated with the operation, if available.</param>
        /// <param name="dataKey">The lobby or member data key associated with the operation, if available.</param>
        /// <param name="requiresHost">True when the operation requires lobby host authority; otherwise, false.</param>
        /// <param name="steamResult">The Steam result code associated with the operation, if available.</param>
        /// <param name="isRetryable">True when retrying later may succeed; otherwise, false.</param>
        public LobbyException(
            string message,
            SteamNetworkErrorKind errorKind,
            string? operation = null,
            CSteamID? lobbyId = null,
            CSteamID? memberId = null,
            string? dataKey = null,
            bool requiresHost = false,
            EResult? steamResult = null,
            bool isRetryable = false)
            : base(message, errorKind, operation, isRetryable)
        {
            LobbyId = lobbyId;
            MemberId = memberId;
            DataKey = dataKey;
            RequiresHost = requiresHost;
            SteamResult = steamResult;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LobbyException"/> class with structured diagnostic details and an inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that caused the current exception.</param>
        /// <param name="errorKind">The broad reason the operation failed.</param>
        /// <param name="operation">The API operation or lifecycle step that failed.</param>
        /// <param name="lobbyId">The Steam ID of the lobby associated with the operation, if available.</param>
        /// <param name="memberId">The Steam ID of the member associated with the operation, if available.</param>
        /// <param name="dataKey">The lobby or member data key associated with the operation, if available.</param>
        /// <param name="requiresHost">True when the operation requires lobby host authority; otherwise, false.</param>
        /// <param name="steamResult">The Steam result code associated with the operation, if available.</param>
        /// <param name="isRetryable">True when retrying later may succeed; otherwise, false.</param>
        public LobbyException(
            string message,
            Exception inner,
            SteamNetworkErrorKind errorKind,
            string? operation = null,
            CSteamID? lobbyId = null,
            CSteamID? memberId = null,
            string? dataKey = null,
            bool requiresHost = false,
            EResult? steamResult = null,
            bool isRetryable = false)
            : base(message, inner, errorKind, operation, isRetryable)
        {
            LobbyId = lobbyId;
            MemberId = memberId;
            DataKey = dataKey;
            RequiresHost = requiresHost;
            SteamResult = steamResult;
        }
    }
}
