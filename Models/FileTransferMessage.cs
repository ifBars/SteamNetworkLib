using System;
using System.Text;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Represents a file transfer message for sharing files between players in chunks.
    /// Supports chunked file transfer to handle large files that exceed network packet size limits.
    /// </summary>
    public class FileTransferMessage : P2PMessage
    {
        /// <summary>
        /// Gets the message type identifier for file transfer messages.
        /// </summary>
        public override string MessageType => "FILE_TRANSFER";

        /// <summary>
        /// Gets or sets the name of the file being transferred.
        /// Should include the file extension for proper identification.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total size of the file in bytes.
        /// Used by the recipient to validate the complete transfer and allocate storage.
        /// </summary>
        public int FileSize { get; set; }

        /// <summary>
        /// Gets or sets the zero-based index of this chunk in the file transfer sequence.
        /// Used to reassemble the file chunks in the correct order.
        /// </summary>
        public int ChunkIndex { get; set; }

        /// <summary>
        /// Gets or sets the total number of chunks that make up the complete file.
        /// Used by the recipient to determine when the file transfer is complete.
        /// </summary>
        public int TotalChunks { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this message contains actual file data.
        /// When false, this message may be a file transfer control message (start, end, error, etc.).
        /// </summary>
        public bool IsFileData { get; set; }

        /// <summary>
        /// Gets or sets the raw file data for this chunk.
        /// Contains the actual bytes of the file segment being transferred.
        /// </summary>
        public byte[] ChunkData { get; set; } = new byte[0];

        /// <summary>
        /// Serializes the file transfer message to a byte array for network transmission.
        /// Uses a hybrid format with JSON header followed by raw binary data.
        /// </summary>
        /// <returns>A byte array containing the serialized message with header and chunk data.</returns>
        public override byte[] Serialize()
        {
            var headerJson = $"{{{CreateJsonBase($"\"FileName\":\"{FileName}\",\"FileSize\":{FileSize},\"ChunkIndex\":{ChunkIndex},\"TotalChunks\":{TotalChunks},\"IsFileData\":{IsFileData.ToString().ToLower()}")}}}";
            var headerBytes = Encoding.UTF8.GetBytes(headerJson);

            // Combine header and chunk data
            var result = new byte[4 + headerBytes.Length + ChunkData.Length];
            var offset = 0;

            // Write header length
            BitConverter.GetBytes(headerBytes.Length).CopyTo(result, offset);
            offset += 4;

            // Write header
            headerBytes.CopyTo(result, offset);
            offset += headerBytes.Length;

            // Write chunk data
            ChunkData.CopyTo(result, offset);

            return result;
        }

        /// <summary>
        /// Deserializes the file transfer message from a byte array received over the network.
        /// Parses the JSON header and extracts the binary chunk data.
        /// </summary>
        /// <param name="data">The byte array containing the serialized message data.</param>
        public override void Deserialize(byte[] data)
        {
            if (data.Length < 4) return;

            var headerLength = BitConverter.ToInt32(data, 0);
            if (headerLength <= 0 || headerLength > data.Length - 4) return;

            var headerJson = Encoding.UTF8.GetString(data, 4, headerLength);
            ParseJsonBase(headerJson);

            FileName = ExtractJsonValue(headerJson, "FileName");

            if (int.TryParse(ExtractJsonValue(headerJson, "FileSize"), out int fileSize))
                FileSize = fileSize;

            if (int.TryParse(ExtractJsonValue(headerJson, "ChunkIndex"), out int chunkIndex))
                ChunkIndex = chunkIndex;

            if (int.TryParse(ExtractJsonValue(headerJson, "TotalChunks"), out int totalChunks))
                TotalChunks = totalChunks;

            IsFileData = ExtractJsonValue(headerJson, "IsFileData").ToLower() == "true";

            // Extract chunk data
            var chunkDataLength = data.Length - 4 - headerLength;
            if (chunkDataLength > 0)
            {
                ChunkData = new byte[chunkDataLength];
                Array.Copy(data, 4 + headerLength, ChunkData, 0, chunkDataLength);
            }
        }
    }
}