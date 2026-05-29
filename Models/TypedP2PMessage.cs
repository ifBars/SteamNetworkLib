using SteamNetworkLib.Sync;
using System;
using System.Text;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Base class for custom P2P messages that carry a strongly typed JSON payload.
    /// </summary>
    /// <typeparam name="TPayload">The payload DTO type to serialize.</typeparam>
    /// <remarks>
    /// Use this for custom mod messages where the message identity and networking metadata
    /// should stay separate from the payload. Payload types should be simple DTOs with a
    /// public parameterless constructor and public get/set properties.
    /// </remarks>
    public abstract class TypedP2PMessage<TPayload> : P2PMessage
    {
        private static readonly JsonSyncSerializer Serializer = new JsonSyncSerializer();

        /// <summary>
        /// Gets or sets the typed payload for this message.
        /// </summary>
        public TPayload Payload { get; set; } = default!;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypedP2PMessage{TPayload}"/> class.
        /// </summary>
        protected TypedP2PMessage()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypedP2PMessage{TPayload}"/> class with a payload.
        /// </summary>
        /// <param name="payload">The payload to send.</param>
        protected TypedP2PMessage(TPayload payload)
        {
            Payload = payload;
        }

        /// <summary>
        /// Serializes this typed message to a byte array for network transmission.
        /// </summary>
        /// <returns>A byte array containing the serialized message data.</returns>
        public override byte[] Serialize()
        {
            try
            {
                string payloadJson = Serializer.Serialize(Payload);
                string json = $"{{{CreateJsonBase($"\"Payload\":{payloadJson}")}}}";
                return Encoding.UTF8.GetBytes(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to serialize typed P2P payload '{typeof(TPayload).Name}' for message '{MessageType}': {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Deserializes this typed message from a byte array received over the network.
        /// </summary>
        /// <param name="data">The byte array containing the serialized message data.</param>
        public override void Deserialize(byte[] data)
        {
            try
            {
                string json = Encoding.UTF8.GetString(data);
                ParseJsonBase(json);

                string payloadJson = ExtractJsonRawValue(json, "Payload");
                if (string.IsNullOrWhiteSpace(payloadJson))
                {
                    Payload = default!;
                    return;
                }

                Payload = Serializer.Deserialize<TPayload>(payloadJson);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize typed P2P payload '{typeof(TPayload).Name}' for message '{MessageType}': {ex.Message}",
                    ex);
            }
        }
    }
}
