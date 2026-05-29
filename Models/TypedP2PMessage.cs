using SteamNetworkLib.Sync;
using System;
using System.Text;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Represents a custom P2P message that carries a strongly typed JSON payload.
    /// </summary>
    /// <typeparam name="TPayload">
    /// The payload DTO type to serialize. Use primitive values, strings, arrays, lists,
    /// dictionaries, and small nested DTOs with public get/set properties.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Derive from this class when a mod needs a custom message type but does not need to
    /// hand-write JSON parsing. The base <see cref="P2PMessage.SenderId"/> and
    /// <see cref="P2PMessage.Timestamp"/> metadata is serialized separately from
    /// <see cref="Payload"/>, so payload contracts can stay focused on gameplay state.
    /// </para>
    /// <para>
    /// This is the preferred shape for transaction and state messages such as restock
    /// requests, label edits, host-authored configuration deltas, and other consumer-mod
    /// messages that need nested DTOs. Do not put <c>CSteamID</c>, Unity objects, item
    /// instances, scene objects, or live storage slot references directly in the payload.
    /// Send stable IDs and primitive values, then resolve runtime objects locally when
    /// handling the message.
    /// </para>
    /// <para>
    /// If you need a custom binary format, compression, encryption, or a serializer not
    /// supported by SteamNetworkLib's sync serializer, inherit from <see cref="P2PMessage"/>
    /// directly instead.
    /// </para>
    /// </remarks>
    public abstract class TypedP2PMessage<TPayload> : P2PMessage
    {
        private static readonly JsonSyncSerializer Serializer = new JsonSyncSerializer();

        /// <summary>
        /// Gets or sets the typed payload carried by this message.
        /// </summary>
        /// <remarks>
        /// The payload is serialized under the <c>Payload</c> JSON property. It should be
        /// non-null before sending unless receivers explicitly handle a default payload.
        /// </remarks>
        public TPayload Payload { get; set; } = default!;

        /// <summary>
        /// Initializes a new instance of the <see cref="TypedP2PMessage{TPayload}"/> class.
        /// </summary>
        /// <remarks>
        /// A public or protected parameterless constructor is required so
        /// <see cref="Utilities.MessageSerializer"/> can create message instances when
        /// packets are received.
        /// </remarks>
        protected TypedP2PMessage()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TypedP2PMessage{TPayload}"/> class with a payload.
        /// </summary>
        /// <param name="payload">The payload to send with the message.</param>
        protected TypedP2PMessage(TPayload payload)
        {
            Payload = payload;
        }

        /// <summary>
        /// Serializes the message metadata and typed payload to UTF-8 JSON.
        /// </summary>
        /// <returns>A byte array containing the serialized message data.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the payload cannot be serialized by SteamNetworkLib's sync serializer.
        /// </exception>
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
        /// Deserializes message metadata and the typed payload from UTF-8 JSON.
        /// </summary>
        /// <param name="data">The byte array containing the serialized message data.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the payload cannot be deserialized into <typeparamref name="TPayload"/>.
        /// </exception>
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
