using System.Text;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Represents a simple text message for P2P communication between players.
    /// This is the most basic message type for sending plain text content.
    /// </summary>
    public class TextMessage : P2PMessage
    {
        /// <summary>
        /// Gets the message type identifier for text messages.
        /// </summary>
        public override string MessageType => "TEXT";

        /// <summary>
        /// Gets or sets the text content of the message.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Serializes the text message to a byte array for network transmission.
        /// </summary>
        /// <returns>A byte array containing the serialized message data in JSON format.</returns>
        public override byte[] Serialize()
        {
            // Escape quotes in content to prevent JSON parsing issues
            var escapedContent = Content.Replace("\"", "\\\"");
            var json = $"{{{CreateJsonBase($"\"Content\":\"{escapedContent}\"")}}}";
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Deserializes the text message from a byte array received over the network.
        /// </summary>
        /// <param name="data">The byte array containing the serialized message data.</param>
        public override void Deserialize(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                ParseJsonBase(json);
                Content = ExtractJsonValue(json, "Content");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[SteamNetworkLib] TextMessage.Deserialize ERROR: {ex.Message}");
                System.Console.WriteLine($"[SteamNetworkLib] TextMessage.Deserialize Stack Trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}