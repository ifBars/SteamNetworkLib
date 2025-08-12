using System;
using System.Text;

namespace SteamNetworkLib.Models
{
    /// <summary>
    /// Represents an event message for broadcasting system events, game events, and notifications between players.
    /// Supports advanced features like priority levels, targeting, acknowledgments, and expiration.
    /// </summary>
    public class EventMessage : P2PMessage
    {
        /// <summary>
        /// Gets the message type identifier for event messages.
        /// </summary>
        public override string MessageType => "EVENT";

        /// <summary>
        /// Gets or sets the type or category of the event.
        /// Common values include "system", "game", "user", "notification", etc.
        /// </summary>
        public string EventType { get; set; } = "user";

        /// <summary>
        /// Gets or sets the specific event name or identifier.
        /// This should be a unique identifier for the specific event being triggered.
        /// </summary>
        public string EventName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the event payload data as a JSON string.
        /// Contains the actual data or parameters associated with the event.
        /// </summary>
        public string EventData { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the priority level of the event.
        /// Values: 0 = low, 1 = normal, 2 = high, 3 = critical. Higher priority events should be processed first.
        /// </summary>
        public int Priority { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating whether this event should be persisted or logged.
        /// Useful for important events that need to be recorded for later analysis.
        /// </summary>
        public bool ShouldPersist { get; set; }

        /// <summary>
        /// Gets or sets the target audience for the event.
        /// Common values include "all", "friends", "specific_players", "host_only", etc.
        /// </summary>
        public string TargetAudience { get; set; } = "all";

        /// <summary>
        /// Gets or sets a comma-separated list of target player Steam IDs.
        /// Only used when <see cref="TargetAudience"/> is set to "specific_players".
        /// </summary>
        public string TargetPlayerIds { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the event requires acknowledgment from recipients.
        /// When true, recipients should send back an acknowledgment message.
        /// </summary>
        public bool RequiresAck { get; set; }

        /// <summary>
        /// Gets or sets the unique event identifier for tracking and acknowledgment purposes.
        /// Automatically generated if not specified.
        /// </summary>
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets when the event expires for time-sensitive events.
        /// Events past their expiration time should be ignored by recipients.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets additional tags for event categorization and filtering.
        /// Can be used to implement custom event filtering logic.
        /// </summary>
        public string Tags { get; set; } = string.Empty;

        /// <summary>
        /// Serializes the event message to a byte array for network transmission.
        /// </summary>
        /// <returns>A byte array containing the serialized message data in JSON format.</returns>
        public override byte[] Serialize()
        {
            var expiresAtStr = ExpiresAt?.ToString("O") ?? "";
            var json = $"{{{CreateJsonBase($"\"EventType\":\"{EventType}\",\"EventName\":\"{EventName}\",\"EventData\":\"{EventData}\",\"Priority\":{Priority},\"ShouldPersist\":{ShouldPersist.ToString().ToLower()},\"TargetAudience\":\"{TargetAudience}\",\"TargetPlayerIds\":\"{TargetPlayerIds}\",\"RequiresAck\":{RequiresAck.ToString().ToLower()},\"EventId\":\"{EventId}\",\"ExpiresAt\":\"{expiresAtStr}\",\"Tags\":\"{Tags}\"")}}}";
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Deserializes the event message from a byte array received over the network.
        /// </summary>
        /// <param name="data">The byte array containing the serialized message data.</param>
        public override void Deserialize(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            ParseJsonBase(json);

            EventType = ExtractJsonValue(json, "EventType");
            EventName = ExtractJsonValue(json, "EventName");
            EventData = ExtractJsonValue(json, "EventData");

            if (int.TryParse(ExtractJsonValue(json, "Priority"), out int priority))
                Priority = priority;

            ShouldPersist = ExtractJsonValue(json, "ShouldPersist").ToLower() == "true";
            TargetAudience = ExtractJsonValue(json, "TargetAudience");
            TargetPlayerIds = ExtractJsonValue(json, "TargetPlayerIds");
            RequiresAck = ExtractJsonValue(json, "RequiresAck").ToLower() == "true";
            EventId = ExtractJsonValue(json, "EventId");

            var expiresAtStr = ExtractJsonValue(json, "ExpiresAt");
            if (!string.IsNullOrEmpty(expiresAtStr) && DateTime.TryParse(expiresAtStr, out DateTime expiresAt))
                ExpiresAt = expiresAt;

            Tags = ExtractJsonValue(json, "Tags");
        }
    }
}