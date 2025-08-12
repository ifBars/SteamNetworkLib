using System.Text;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Represents a data synchronization message for sharing key-value data between players.
    /// Used for synchronizing game state, configuration data, or any structured information across the lobby.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For simple, small data synchronization (typically under 1KB), consider using lobby data or member data instead:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Use <see cref="SteamNetworkLib.Core.SteamLobbyData"/> for lobby-wide data that all players need to see</description></item>
    /// <item><description>Use <see cref="SteamNetworkLib.Core.SteamMemberData"/> for player-specific data that should be visible to all players</description></item>
    /// </list>
    /// </remarks>
    public class DataSyncMessage : P2PMessage
    {
        /// <summary>
        /// Gets the message type identifier for data sync messages.
        /// </summary>
        public override string MessageType => "DATA_SYNC";

        /// <summary>
        /// Gets or sets the data key identifier.
        /// This key is used to identify what type of data is being synchronized.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the data value to be synchronized.
        /// Can contain any string data including JSON, XML, or plain text.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the data type identifier that describes the format of the value.
        /// Common values include "string", "json", "xml", "binary", etc.
        /// </summary>
        public string DataType { get; set; } = "string";

        /// <summary>
        /// Serializes the data sync message to a byte array for network transmission.
        /// </summary>
        /// <returns>A byte array containing the serialized message data in JSON format.</returns>
        public override byte[] Serialize()
        {
#if IL2CPP && SCHEDULE_ONE_INTEGRATION
            System.Console.WriteLine($"[IL2CPP] DataSyncMessage.Serialize: Key='{Key}', DataType='{DataType}'");
            if (Value.Length > 100)
            {
                System.Console.WriteLine($"[IL2CPP] Value (first 100 chars): {Value.Substring(0, 100)}...");
            }
            else
            {
                System.Console.WriteLine($"[IL2CPP] Value: {Value}");
            }
#endif
            // Escape quotes in key and value to prevent JSON parsing issues
            var escapedKey = Key.Replace("\"", "\\\"");
            var escapedValue = Value.Replace("\"", "\\\"");
            var escapedDataType = DataType.Replace("\"", "\\\"");
            
            var json = $"{{{CreateJsonBase($"\"Key\":\"{escapedKey}\",\"Value\":\"{escapedValue}\",\"DataType\":\"{escapedDataType}\"")}}}";
#if IL2CPP && SCHEDULE_ONE_INTEGRATION
            System.Console.WriteLine($"[IL2CPP] DataSyncMessage.Serialize: JSON length={json.Length}");
#endif
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Deserializes the data sync message from a byte array received over the network.
        /// </summary>
        /// <param name="data">The byte array containing the serialized message data.</param>
        public override void Deserialize(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
#if IL2CPP && SCHEDULE_ONE_INTEGRATION
                System.Console.WriteLine($"[IL2CPP] DataSyncMessage.Deserialize: JSON length={json.Length}");
                if (json.Length > 200)
                {
                    System.Console.WriteLine($"[IL2CPP] JSON (first 200 chars): {json.Substring(0, 200)}...");
                }
                else
                {
                    System.Console.WriteLine($"[IL2CPP] JSON: {json}");
                }
#endif
                ParseJsonBase(json);
                Key = ExtractJsonValue(json, "Key");
                Value = ExtractJsonValue(json, "Value");
                DataType = ExtractJsonValue(json, "DataType");
#if IL2CPP && SCHEDULE_ONE_INTEGRATION
                System.Console.WriteLine($"[IL2CPP] DataSyncMessage.Deserialize: Extracted Key='{Key}', DataType='{DataType}'");
                if (Value.Length > 100)
                {
                    System.Console.WriteLine($"[IL2CPP] Extracted Value (first 100 chars): {Value.Substring(0, 100)}...");
                }
                else
                {
                    System.Console.WriteLine($"[IL2CPP] Extracted Value: {Value}");
                }
#endif
            }
            catch (System.Exception ex)
            {
#if IL2CPP && SCHEDULE_ONE_INTEGRATION
                System.Console.WriteLine($"[IL2CPP] DataSyncMessage.Deserialize ERROR: {ex.Message}");
                System.Console.WriteLine($"[IL2CPP] DataSyncMessage.Deserialize ERROR Stack: {ex.StackTrace}");
                
                // Try to show the raw data for debugging
                try {
                    var jsonStr = Encoding.UTF8.GetString(data);
                    System.Console.WriteLine($"[IL2CPP] Raw JSON: {jsonStr}");
                } catch {}
#endif
                throw;
            }
        }
    }
}