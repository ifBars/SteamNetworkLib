namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Base class for typed P2P response messages with a correlation ID.
    /// </summary>
    /// <typeparam name="TPayload">The response payload DTO type.</typeparam>
    public abstract class P2PResponseMessage<TPayload> : TypedP2PMessage<P2PResponsePayload<TPayload>>, IP2PCorrelatedMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="P2PResponseMessage{TPayload}"/> class.
        /// </summary>
        protected P2PResponseMessage()
            : base(new P2PResponsePayload<TPayload>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PResponseMessage{TPayload}"/> class.
        /// </summary>
        /// <param name="payload">The response payload.</param>
        protected P2PResponseMessage(TPayload payload)
            : base(new P2PResponsePayload<TPayload> { Body = payload })
        {
        }

        /// <inheritdoc />
        public string RequestId
        {
            get => Payload.RequestId;
            set => Payload.RequestId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets whether the request completed successfully.
        /// </summary>
        public bool Success
        {
            get => Payload.Success;
            set => Payload.Success = value;
        }

        /// <summary>
        /// Gets or sets a developer-facing error message when <see cref="Success"/> is false.
        /// </summary>
        public string Error
        {
            get => Payload.Error;
            set => Payload.Error = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the response body.
        /// </summary>
        public TPayload Body
        {
            get => Payload.Body;
            set => Payload.Body = value;
        }
    }

    /// <summary>
    /// Serializable response envelope used by <see cref="P2PResponseMessage{TPayload}"/>.
    /// </summary>
    /// <typeparam name="TPayload">The response body type.</typeparam>
    public class P2PResponsePayload<TPayload>
    {
        /// <summary>
        /// Gets or sets the request correlation identifier.
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the request completed successfully.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Gets or sets a developer-facing error message.
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the response body.
        /// </summary>
        public TPayload Body { get; set; } = default!;
    }
}
