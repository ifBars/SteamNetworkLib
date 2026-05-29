#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;

namespace SteamNetworkLib.Exceptions
{
    /// <summary>
    /// Exception thrown when peer-to-peer (P2P) communication operations fail.
    /// Provides additional context about Steam P2P operations including target IDs, session errors, and channels.
    /// </summary>
    public class P2PException : SteamNetworkException
    {
        /// <summary>
        /// Gets the Steam ID of the target peer associated with the P2P operation, if available.
        /// </summary>
        public CSteamID? TargetId { get; }

        /// <summary>
        /// Gets the target peer Steam ID as a 64-bit integer, or 0 when no target ID is available.
        /// </summary>
        public ulong TargetId64 => TargetId?.m_SteamID ?? 0UL;

        /// <summary>
        /// Gets the P2P session error that occurred during the operation, if available.
        /// </summary>
        public EP2PSessionError? SessionError { get; }

        /// <summary>
        /// Gets the communication channel associated with the P2P operation.
        /// </summary>
        public int Channel { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Channel"/> was provided for the failed operation.
        /// </summary>
        public bool HasChannel { get; }

        /// <summary>
        /// Gets the message type associated with the failed operation, if available.
        /// </summary>
        public string? MessageType { get; }

        /// <summary>
        /// Gets the packet size in bytes associated with the failed operation, if available.
        /// </summary>
        public int? PacketSize { get; }

        /// <summary>
        /// Gets the maximum allowed packet size in bytes, if available.
        /// </summary>
        public int? MaxPacketSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PException"/> class.
        /// </summary>
        public P2PException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public P2PException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public P2PException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PException"/> class with a specified error message and target peer ID.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="targetId">The Steam ID of the target peer associated with the operation.</param>
        public P2PException(string message, CSteamID targetId) : base(message)
        {
            TargetId = targetId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PException"/> class with a specified error message and session error.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="sessionError">The P2P session error that occurred during the operation.</param>
        public P2PException(string message, EP2PSessionError sessionError) : base(message)
        {
            SessionError = sessionError;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PException"/> class with a specified error message, target peer ID, and channel.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="targetId">The Steam ID of the target peer associated with the operation.</param>
        /// <param name="channel">The communication channel associated with the P2P operation.</param>
        public P2PException(string message, CSteamID targetId, int channel) : base(message)
        {
            TargetId = targetId;
            Channel = channel;
            HasChannel = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PException"/> class with a specified error message, target peer ID, and session error.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="targetId">The Steam ID of the target peer associated with the operation.</param>
        /// <param name="sessionError">The P2P session error that occurred during the operation.</param>
        public P2PException(string message, CSteamID targetId, EP2PSessionError sessionError) : base(message)
        {
            TargetId = targetId;
            SessionError = sessionError;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PException"/> class with structured diagnostic details.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorKind">The broad reason the operation failed.</param>
        /// <param name="operation">The API operation or lifecycle step that failed.</param>
        /// <param name="targetId">The Steam ID of the target peer associated with the operation, if available.</param>
        /// <param name="channel">The communication channel associated with the operation, if available.</param>
        /// <param name="messageType">The message type associated with the operation, if available.</param>
        /// <param name="packetSize">The packet size in bytes associated with the operation, if available.</param>
        /// <param name="maxPacketSize">The maximum allowed packet size in bytes, if available.</param>
        /// <param name="sessionError">The P2P session error associated with the operation, if available.</param>
        /// <param name="isRetryable">True when retrying later may succeed; otherwise, false.</param>
        public P2PException(
            string message,
            SteamNetworkErrorKind errorKind,
            string? operation = null,
            CSteamID? targetId = null,
            int? channel = null,
            string? messageType = null,
            int? packetSize = null,
            int? maxPacketSize = null,
            EP2PSessionError? sessionError = null,
            bool isRetryable = false)
            : base(message, errorKind, operation, isRetryable)
        {
            TargetId = targetId;
            Channel = channel ?? 0;
            HasChannel = channel.HasValue;
            MessageType = messageType;
            PacketSize = packetSize;
            MaxPacketSize = maxPacketSize;
            SessionError = sessionError;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PException"/> class with structured diagnostic details and an inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that caused the current exception.</param>
        /// <param name="errorKind">The broad reason the operation failed.</param>
        /// <param name="operation">The API operation or lifecycle step that failed.</param>
        /// <param name="targetId">The Steam ID of the target peer associated with the operation, if available.</param>
        /// <param name="channel">The communication channel associated with the operation, if available.</param>
        /// <param name="messageType">The message type associated with the operation, if available.</param>
        /// <param name="packetSize">The packet size in bytes associated with the operation, if available.</param>
        /// <param name="maxPacketSize">The maximum allowed packet size in bytes, if available.</param>
        /// <param name="sessionError">The P2P session error associated with the operation, if available.</param>
        /// <param name="isRetryable">True when retrying later may succeed; otherwise, false.</param>
        public P2PException(
            string message,
            Exception inner,
            SteamNetworkErrorKind errorKind,
            string? operation = null,
            CSteamID? targetId = null,
            int? channel = null,
            string? messageType = null,
            int? packetSize = null,
            int? maxPacketSize = null,
            EP2PSessionError? sessionError = null,
            bool isRetryable = false)
            : base(message, inner, errorKind, operation, isRetryable)
        {
            TargetId = targetId;
            Channel = channel ?? 0;
            HasChannel = channel.HasValue;
            MessageType = messageType;
            PacketSize = packetSize;
            MaxPacketSize = maxPacketSize;
            SessionError = sessionError;
        }
    }
}
