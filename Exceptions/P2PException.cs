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
        /// Gets the P2P session error that occurred during the operation, if available.
        /// </summary>
        public EP2PSessionError? SessionError { get; }

        /// <summary>
        /// Gets the communication channel associated with the P2P operation.
        /// </summary>
        public int Channel { get; }

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
    }
}