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
        /// Gets the broad reason the operation failed.
        /// </summary>
        public SteamNetworkErrorKind ErrorKind { get; }

        /// <summary>
        /// Gets the API operation or lifecycle step that failed, if available.
        /// </summary>
        public string? Operation { get; }

        /// <summary>
        /// Gets a value indicating whether retrying the operation later may succeed.
        /// </summary>
        public bool IsRetryable { get; }

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

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamNetworkException"/> class with structured diagnostic details.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errorKind">The broad reason the operation failed.</param>
        /// <param name="operation">The API operation or lifecycle step that failed.</param>
        /// <param name="isRetryable">True when retrying later may succeed; otherwise, false.</param>
        public SteamNetworkException(
            string message,
            SteamNetworkErrorKind errorKind,
            string? operation = null,
            bool isRetryable = false)
            : base(message)
        {
            ErrorKind = errorKind;
            Operation = operation;
            IsRetryable = isRetryable;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SteamNetworkException"/> class with structured diagnostic details and an inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="inner">The exception that caused the current exception.</param>
        /// <param name="errorKind">The broad reason the operation failed.</param>
        /// <param name="operation">The API operation or lifecycle step that failed.</param>
        /// <param name="isRetryable">True when retrying later may succeed; otherwise, false.</param>
        public SteamNetworkException(
            string message,
            Exception inner,
            SteamNetworkErrorKind errorKind,
            string? operation = null,
            bool isRetryable = false)
            : base(message, inner)
        {
            ErrorKind = errorKind;
            Operation = operation;
            IsRetryable = isRetryable;
        }
    }
}
