namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Base class for typed P2P request messages with a correlation ID.
    /// </summary>
    /// <typeparam name="TPayload">The request payload DTO type.</typeparam>
    /// <remarks>
    /// Derive from this class for client-to-host or peer-to-peer actions where the sender
    /// expects one response. Set the message type in the derived type and store
    /// gameplay data in the typed payload. The request/response coordinator fills
    /// <see cref="RequestId"/> automatically when it is empty.
    /// </remarks>
    public abstract class P2PRequestMessage<TPayload> : TypedP2PMessage<P2PRequestPayload<TPayload>>, IP2PCorrelatedMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="P2PRequestMessage{TPayload}"/> class.
        /// </summary>
        protected P2PRequestMessage()
            : base(new P2PRequestPayload<TPayload>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PRequestMessage{TPayload}"/> class.
        /// </summary>
        /// <param name="payload">The request payload.</param>
        protected P2PRequestMessage(TPayload payload)
            : base(new P2PRequestPayload<TPayload> { Body = payload })
        {
        }

        /// <inheritdoc />
        public string RequestId
        {
            get => Payload.RequestId;
            set => Payload.RequestId = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the request body.
        /// </summary>
        public TPayload Body
        {
            get => Payload.Body;
            set => Payload.Body = value;
        }
    }

    /// <summary>
    /// Serializable request envelope used by <see cref="P2PRequestMessage{TPayload}"/>.
    /// </summary>
    /// <typeparam name="TPayload">The request body type.</typeparam>
    public class P2PRequestPayload<TPayload>
    {
        /// <summary>
        /// Gets or sets the request correlation identifier.
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request body.
        /// </summary>
        public TPayload Body { get; set; } = default!;
    }
}
