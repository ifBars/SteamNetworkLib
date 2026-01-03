using System;
using System.Text;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Represents a heartbeat message for connection monitoring, latency measurement, and keepalive functionality.
    /// Used to monitor P2P network performance.
    /// </summary>
    public class HeartbeatMessage : P2PMessage
    {
        /// <summary>
        /// Gets the message type identifier for heartbeat messages.
        /// </summary>
        public override string MessageType => "HEARTBEAT";

        /// <summary>
        /// Gets or sets the unique identifier for this heartbeat.
        /// Used for matching ping and pong messages to calculate round-trip time.
        /// </summary>
        public string HeartbeatId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets a value indicating whether this is a response to a heartbeat (pong).
        /// When false, this is an initial heartbeat (ping). When true, this is a response (pong).
        /// </summary>
        public bool IsResponse { get; set; }

        /// <summary>
        /// Gets or sets the high-precision timestamp when this heartbeat was sent.
        /// Used for accurate latency calculations using system ticks or similar high-resolution timing.
        /// </summary>
        public long HighPrecisionTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the sequence number for tracking heartbeat order.
        /// Helps detect lost heartbeats and measure packet ordering.
        /// </summary>
        public uint SequenceNumber { get; set; }

        /// <summary>
        /// Gets or sets the connection quality information as packet loss percentage.
        /// Value between 0.0 and 100.0 indicating the percentage of packets lost.
        /// </summary>
        public float PacketLossPercent { get; set; }

        /// <summary>
        /// Gets or sets the average latency in milliseconds.
        /// Calculated from recent ping/pong round-trip times.
        /// </summary>
        public float AverageLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the current bandwidth usage in bytes per second.
        /// Includes both incoming and outgoing data transfer rates.
        /// </summary>
        public int BandwidthUsage { get; set; }

        /// <summary>
        /// Gets or sets the current player status or state.
        /// Common values include "online", "away", "busy", "playing", etc.
        /// </summary>
        public string PlayerStatus { get; set; } = "online";

        /// <summary>
        /// Gets or sets additional metadata about the player's connection.
        /// Can include information like connection type, NAT status, or other network details.
        /// </summary>
        public string ConnectionInfo { get; set; } = string.Empty;

        /// <summary>
        /// Serializes the heartbeat message to a byte array for network transmission.
        /// </summary>
        /// <returns>A byte array containing the serialized message data in JSON format.</returns>
        public override byte[] Serialize()
        {
            // Set high precision timestamp if not already set
            if (HighPrecisionTimestamp == 0)
            {
                HighPrecisionTimestamp = DateTime.UtcNow.Ticks;
            }

            var json = $"{{{CreateJsonBase($"\"HeartbeatId\":\"{HeartbeatId}\",\"IsResponse\":{IsResponse.ToString().ToLower()},\"HighPrecisionTimestamp\":{HighPrecisionTimestamp},\"SequenceNumber\":{SequenceNumber},\"PacketLossPercent\":{PacketLossPercent},\"AverageLatencyMs\":{AverageLatencyMs},\"BandwidthUsage\":{BandwidthUsage},\"PlayerStatus\":\"{PlayerStatus}\",\"ConnectionInfo\":\"{ConnectionInfo}\"")}}}";
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Deserializes the heartbeat message from a byte array received over the network.
        /// </summary>
        /// <param name="data">The byte array containing the serialized message data.</param>
        public override void Deserialize(byte[] data)
        {
            try
            {
                var json = Encoding.UTF8.GetString(data);
                ParseJsonBase(json);

                HeartbeatId = ExtractJsonValue(json, "HeartbeatId");
                IsResponse = ExtractJsonValue(json, "IsResponse").ToLower() == "true";

                if (long.TryParse(ExtractJsonValue(json, "HighPrecisionTimestamp"), out long timestamp))
                    HighPrecisionTimestamp = timestamp;

                if (uint.TryParse(ExtractJsonValue(json, "SequenceNumber"), out uint seqNum))
                    SequenceNumber = seqNum;

                if (float.TryParse(ExtractJsonValue(json, "PacketLossPercent"), out float packetLoss))
                    PacketLossPercent = packetLoss;

                if (float.TryParse(ExtractJsonValue(json, "AverageLatencyMs"), out float latency))
                    AverageLatencyMs = latency;

                if (int.TryParse(ExtractJsonValue(json, "BandwidthUsage"), out int bandwidth))
                    BandwidthUsage = bandwidth;

                PlayerStatus = ExtractJsonValue(json, "PlayerStatus");
                ConnectionInfo = ExtractJsonValue(json, "ConnectionInfo");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamNetworkLib] HeartbeatMessage.Deserialize ERROR: {ex.Message}");
                Console.WriteLine($"[SteamNetworkLib] HeartbeatMessage.Deserialize Stack Trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}