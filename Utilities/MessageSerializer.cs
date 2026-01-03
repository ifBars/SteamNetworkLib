using SteamNetworkLib.Exceptions;
using SteamNetworkLib.Models;
using System;
using System.Text;

namespace SteamNetworkLib.Utilities
{
    /// <summary>
    /// Utility methods for message serialization and deserialization.
    /// </summary>
    public static class MessageSerializer
    {
        private const int MAX_MESSAGE_SIZE = 1024 * 4; // 4KB max for Steam P2P packets
        
        /// <summary>
        /// Header identifier for SteamNetworkLib messages to validate message authenticity.
        /// </summary>
        public const string MESSAGE_HEADER = "SNLM"; // SteamNetworkLib Message header

        /// <summary>
        /// Serializes a P2P message to a byte array with header validation.
        /// </summary>
        /// <param name="message">The message to serialize.</param>
        /// <returns>Serialized message data.</returns>
        /// <exception cref="P2PException">Thrown when serialization fails or message is too large.</exception>
        public static byte[] SerializeMessage(P2PMessage message)
        {
            try
            {
                var messageData = message.Serialize();
                var messageTypeBytes = Encoding.UTF8.GetBytes(message.MessageType);
                var headerBytes = Encoding.UTF8.GetBytes(MESSAGE_HEADER);

                // Format: [HEADER(4)][TYPE_LENGTH(1)][TYPE][MESSAGE_DATA]
                var totalSize = headerBytes.Length + 1 + messageTypeBytes.Length + messageData.Length;

                if (totalSize > MAX_MESSAGE_SIZE)
                {
                    throw new P2PException($"Message too large: {totalSize} bytes (max: {MAX_MESSAGE_SIZE})");
                }

                var result = new byte[totalSize];
                var offset = 0;

                // Copy header
                Array.Copy(headerBytes, 0, result, offset, headerBytes.Length);
                offset += headerBytes.Length;

                // Copy type length
                result[offset] = (byte)messageTypeBytes.Length;
                offset++;

                // Copy type
                Array.Copy(messageTypeBytes, 0, result, offset, messageTypeBytes.Length);
                offset += messageTypeBytes.Length;

                // Copy message data
                Array.Copy(messageData, 0, result, offset, messageData.Length);

                return result;
            }
            catch (Exception ex) when (!(ex is P2PException))
            {
                throw new P2PException($"Failed to serialize message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deserializes a byte array to extract message type and data.
        /// </summary>
        /// <param name="data">The serialized data.</param>
        /// <returns>Tuple containing message type and message data.</returns>
        /// <exception cref="P2PException">Thrown when deserialization fails or data is invalid.</exception>
        public static (string MessageType, byte[] MessageData) DeserializeMessage(byte[] data)
        {
            try
            {
                if (data.Length < 6) // Minimum size: header(4) + type_length(1) + type(1)
                {
                    var errorMsg = $"Invalid message data: too short ({data.Length} bytes)";
                    throw new P2PException(errorMsg);
                }

                var headerBytes = Encoding.UTF8.GetBytes(MESSAGE_HEADER);
                var offset = 0;

                // Validate header
                bool headerValid = true;
                for (int i = 0; i < headerBytes.Length; i++)
                {
                    if (data[offset + i] != headerBytes[i])
                    {
                        headerValid = false;
                        break;
                    }
                }
                
                if (!headerValid)
                {
                    var actualHeader = new byte[headerBytes.Length];
                    Array.Copy(data, offset, actualHeader, 0, headerBytes.Length);
                    var errorMsg = $"Invalid message header: expected '{MESSAGE_HEADER}', got '{Encoding.UTF8.GetString(actualHeader)}'";
                    throw new P2PException(errorMsg);
                }
                offset += headerBytes.Length;

                // Get type length
                var typeLength = data[offset];
                offset++;

                if (typeLength == 0 || offset + typeLength > data.Length)
                {
                    var errorMsg = $"Invalid message format: type length {typeLength} exceeds data bounds (data length: {data.Length}, offset: {offset})";
                    throw new P2PException(errorMsg);
                }

                // Get message type
                var messageType = Encoding.UTF8.GetString(data, offset, typeLength);
                offset += typeLength;

                // Get message data
                var messageDataLength = data.Length - offset;
                var messageData = new byte[messageDataLength];
                Array.Copy(data, offset, messageData, 0, messageDataLength);

                return (messageType, messageData);
            }
            catch (Exception ex) when (!(ex is P2PException))
            {
                throw new P2PException($"Failed to deserialize message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a P2P message instance from serialized data.
        /// </summary>
        /// <typeparam name="T">The message type to create.</typeparam>
        /// <param name="data">The serialized data.</param>
        /// <returns>Deserialized message instance.</returns>
        /// <exception cref="P2PException">Thrown when creation fails or type mismatch occurs.</exception>
        public static T CreateMessage<T>(byte[] data) where T : P2PMessage, new()
        {
            try
            {
                var (messageType, messageData) = DeserializeMessage(data);

                var message = new T();

                // Validate message type matches
                if (message.MessageType != messageType)
                {
                    throw new P2PException($"Message type mismatch: expected '{message.MessageType}', got '{messageType}'");
                }

                message.Deserialize(messageData);

                return message;
            }
            catch (Exception ex) when (!(ex is P2PException))
            {
                throw new P2PException($"Failed to create message of type {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates that data contains a valid SteamNetworkLib message.
        /// </summary>
        /// <param name="data">The data to validate.</param>
        /// <returns>True if the data contains a valid message.</returns>
        public static bool IsValidMessage(byte[] data)
        {
            try
            {
                if (data.Length < 6)
                {
                    return false;
                }

                var headerBytes = Encoding.UTF8.GetBytes(MESSAGE_HEADER);
                
                // Check header
                bool headerValid = true;
                for (int i = 0; i < headerBytes.Length; i++)
                {
                    if (data[i] != headerBytes[i])
                    {
                        headerValid = false;
                        break;
                    }
                }
                
                if (!headerValid)
                {
                    return false;
                }

                var typeLength = data[headerBytes.Length];
                bool valid = headerBytes.Length + 1 + typeLength < data.Length;
                
                return valid;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamNetworkLib] MessageSerializer.IsValidMessage ERROR: {ex.Message}");
                Console.WriteLine($"[SteamNetworkLib] MessageSerializer.IsValidMessage Stack Trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Gets the message type from serialized data without full deserialization.
        /// </summary>
        /// <param name="data">The serialized data.</param>
        /// <returns>Message type string, or null if invalid.</returns>
        public static string? GetMessageType(byte[] data)
        {
            try
            {
                if (!IsValidMessage(data))
                {
                    return null;
                }

                var headerBytes = Encoding.UTF8.GetBytes(MESSAGE_HEADER);
                var offset = headerBytes.Length;
                var typeLength = data[offset];
                offset++;

                if (offset + typeLength > data.Length)
                {
                    return null;
                }

                var messageType = Encoding.UTF8.GetString(data, offset, typeLength);
                return messageType;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamNetworkLib] MessageSerializer.GetMessageType ERROR: {ex.Message}");
                Console.WriteLine($"[SteamNetworkLib] MessageSerializer.GetMessageType Stack Trace: {ex.StackTrace}");
                return null;
            }
        }

#if IL2CPP
        /// <summary>
        /// Helper method to convert byte array to hex string for debugging
        /// </summary>
        public static string BytesToHexString(byte[] bytes)
        {
            var hex = new StringBuilder(bytes.Length * 3);
            foreach (byte b in bytes)
            {
                hex.Append(b.ToString("X2"));
                hex.Append(' ');
            }
            return hex.ToString();
        }
#endif
    }
}