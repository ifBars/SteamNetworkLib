#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Text;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Base class for all P2P messages in SteamNetworkLib.
    /// Provides common functionality for serialization, deserialization, and message metadata.
    /// </summary>
    public abstract class P2PMessage
    {
        /// <summary>
        /// Gets the unique identifier for this message type.
        /// Must be implemented by derived classes to specify the message type.
        /// </summary>
        public abstract string MessageType { get; }

        /// <summary>
        /// Gets or sets the Steam ID of the player who sent this message.
        /// This is automatically set when sending messages through the P2P manager.
        /// </summary>
        public CSteamID SenderId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this message was created.
        /// Defaults to UTC time when the message instance is created.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Serializes the message to a byte array for network transmission.
        /// Must be implemented by derived classes to handle their specific data.
        /// </summary>
        /// <returns>A byte array containing the serialized message data.</returns>
        public abstract byte[] Serialize();

        /// <summary>
        /// Deserializes the message from a byte array received over the network.
        /// Must be implemented by derived classes to reconstruct their specific data.
        /// </summary>
        /// <param name="data">The byte array containing the serialized message data.</param>
        /// TODO: Implement deserialization exception
        public abstract void Deserialize(byte[] data);

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PMessage"/> class.
        /// Sets the timestamp to the current UTC time.
        /// </summary>
        protected P2PMessage()
        {
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Helper method to create a JSON representation of basic message properties.
        /// Can be extended by derived classes to include their specific properties.
        /// </summary>
        /// <param name="additionalData">Additional JSON data to include in the base JSON string.</param>
        /// <returns>A JSON string containing the base message properties and any additional data.</returns>
        protected string CreateJsonBase(string additionalData = "")
        {
#if IL2CPP
            // Ensure SenderId is valid in IL2CPP
            ulong steamId = SenderId.m_SteamID;
            if (steamId == 0)
            {
                steamId = 76561197960265728; // Use a placeholder value if empty
            }
            
            var baseJson = $"\"SenderId\":{steamId},\"Timestamp\":\"{Timestamp:O}\"";
#else
            var baseJson = $"\"SenderId\":{SenderId.m_SteamID},\"Timestamp\":\"{Timestamp:O}\"";
#endif
            return string.IsNullOrEmpty(additionalData) ? baseJson : $"{baseJson},{additionalData}";
        }

        /// <summary>
        /// Helper method to parse basic message properties from JSON.
        /// Should be called by derived classes to populate the base properties.
        /// </summary>
        /// <param name="json">The JSON string to parse.</param>
        protected void ParseJsonBase(string json)
        {
            try
            {
                string senderIdStr = ExtractJsonValue(json, "SenderId");

                if (ulong.TryParse(senderIdStr, out ulong senderId))
                {
                    SenderId = new CSteamID(senderId);
                }

                string timestampStr = ExtractJsonValue(json, "Timestamp");

                if (DateTime.TryParse(timestampStr, out DateTime timestamp))
                {
                    Timestamp = timestamp;
                }
                else
                {
                    // Use current time as fallback
                    Timestamp = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                // Use default values as fallback
                Console.WriteLine($"[SteamNetworkLib] P2PMessage.ParseJsonBase ERROR: {ex.Message}");
                Console.WriteLine($"[SteamNetworkLib] P2PMessage.ParseJsonBase Stack Trace: {ex.StackTrace}");
                SenderId = new CSteamID(76561197960265728); // Placeholder
                Timestamp = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Simple JSON value extractor that avoids dependencies on external JSON libraries.
        /// Handles both quoted strings and unquoted values (numbers, booleans).
        /// </summary>
        /// <param name="json">The JSON string to extract the value from.</param>
        /// <param name="key">The key of the value to extract.</param>
        /// <returns>The extracted value as a string, or an empty string if not found.</returns>
        protected string ExtractJsonValue(string json, string key)
        {
            try
            {
                var keyPattern = $"\"{key}\":";
                var startIndex = json.IndexOf(keyPattern);
                if (startIndex == -1)
                {
                    return string.Empty;
                }

                startIndex += keyPattern.Length;

                while (startIndex < json.Length && char.IsWhiteSpace(json[startIndex]))
                    startIndex++;

                if (startIndex >= json.Length)
                {
                    return string.Empty;
                }

                if (json[startIndex] == '"')
                {
                    // Handle quoted string value
                    startIndex++;
                    int endIndex = -1;
                    bool escaped = false;
                    
                    // Find the closing quote, handling escaped quotes
                    for (int i = startIndex; i < json.Length; i++)
                    {
                        if (json[i] == '\\' && !escaped)
                        {
                            escaped = true;
                            continue;
                        }
                        
                        if (json[i] == '"' && !escaped)
                        {
                            endIndex = i;
                            break;
                        }
                        
                        escaped = false;
                    }
                    
                    if (endIndex == -1)
                    {
                        return string.Empty;
                    }
                    
                    string value = json.Substring(startIndex, endIndex - startIndex);
                    
                    // Unescape special characters
                    value = value.Replace("\\\"", "\"")
                               .Replace("\\\\", "\\")
                               .Replace("\\/", "/")
                               .Replace("\\b", "\b")
                               .Replace("\\f", "\f")
                               .Replace("\\n", "\n")
                               .Replace("\\r", "\r")
                               .Replace("\\t", "\t");
                    
                    return value;
                }
                else
                {
                    // Handle unquoted value (number, boolean, null)
                    var endIndex = startIndex;
                    while (endIndex < json.Length &&
                           json[endIndex] != ',' &&
                           json[endIndex] != '}' &&
                           json[endIndex] != ']' &&
                           !char.IsWhiteSpace(json[endIndex]))
                    {
                        endIndex++;
                    }
                    
                    string value = json.Substring(startIndex, endIndex - startIndex);
                    return value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamNetworkLib] P2PMessage.ExtractJsonValue ERROR: {ex.Message}");
                Console.WriteLine($"[SteamNetworkLib] P2PMessage.ExtractJsonValue Stack Trace: {ex.StackTrace}");
                return string.Empty;
            }
        }
    }
}