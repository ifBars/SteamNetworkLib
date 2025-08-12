using System;

namespace SteamNetworkLib.Exceptions
{
    /// <summary>
    /// Base exception for all Steam networking operations in SteamNetworkLib.
    /// This serves as the parent class for more specific networking exceptions.
    /// </summary>
    public class SteamNetworkException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SteamNetworkException"/> class.
        /// </summary>
        public SteamNetworkException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamNetworkException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SteamNetworkException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamNetworkException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public SteamNetworkException(string message, Exception inner) : base(message, inner) { }
    }
}