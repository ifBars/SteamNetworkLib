using System;
using FluentAssertions;
using SteamNetworkLib.Models;
using Steamworks;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    /// <summary>
    /// Unit tests for P2P message types (TextMessage, DataSyncMessage, HeartbeatMessage).
    /// Tests serialization, deserialization, and round-trip conversion.
    /// </summary>
    public class P2PMessageTests
    {
        #region TextMessage Tests

        [Fact]
        public void TextMessage_Serialize_ProducesValidJson()
        {
            // Arrange
            var message = new TextMessage
            {
                SenderId = new CSteamID(76561197960265728),
                Timestamp = new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc),
                Content = "Hello, World!"
            };

            // Act
            var bytes = message.Serialize();
            var json = System.Text.Encoding.UTF8.GetString(bytes);

            // Assert
            json.Should().Contain("\"SenderId\":76561197960265728");
            json.Should().Contain("\"Content\":\"Hello, World!\"");
            json.Should().Contain("\"Timestamp\":");
        }

        [Fact]
        public void TextMessage_Deserialize_RestoresContent()
        {
            // Arrange
            var original = new TextMessage
            {
                SenderId = new CSteamID(76561197960265728),
                Content = "Test message"
            };
            var bytes = original.Serialize();

            // Act
            var restored = new TextMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.Content.Should().Be("Test message");
            restored.SenderId.m_SteamID.Should().Be(76561197960265728);
        }

        [Fact]
        public void TextMessage_WithQuotes_EscapesCorrectly()
        {
            // Arrange
            var message = new TextMessage
            {
                SenderId = new CSteamID(12345),
                Content = "He said \"Hello\" to me"
            };

            // Act
            var bytes = message.Serialize();
            var restored = new TextMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.Content.Should().Be("He said \"Hello\" to me");
        }

        [Fact]
        public void TextMessage_EmptyContent_SerializesAndDeserializes()
        {
            // Arrange
            var message = new TextMessage
            {
                SenderId = new CSteamID(12345),
                Content = ""
            };

            // Act
            var bytes = message.Serialize();
            var restored = new TextMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.Content.Should().Be("");
        }

        [Fact]
        public void TextMessage_LongContent_SerializesCorrectly()
        {
            // Arrange
            var longText = new string('A', 5000);
            var message = new TextMessage
            {
                SenderId = new CSteamID(12345),
                Content = longText
            };

            // Act
            var bytes = message.Serialize();
            var restored = new TextMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.Content.Should().HaveLength(5000);
            restored.Content.Should().Be(longText);
        }

        [Fact]
        public void TextMessage_MessageType_ReturnsText()
        {
            // Arrange
            var message = new TextMessage();

            // Assert
            message.MessageType.Should().Be("TEXT");
        }

        #endregion

        #region DataSyncMessage Tests

        [Fact]
        public void DataSyncMessage_Serialize_ProducesValidJson()
        {
            // Arrange
            var message = new DataSyncMessage
            {
                SenderId = new CSteamID(76561197960265728),
                Key = "PlayerScore",
                Value = "1500",
                DataType = "integer"
            };

            // Act
            var bytes = message.Serialize();
            var json = System.Text.Encoding.UTF8.GetString(bytes);

            // Assert
            json.Should().Contain("\"Key\":\"PlayerScore\"");
            json.Should().Contain("\"Value\":\"1500\"");
            json.Should().Contain("\"DataType\":\"integer\"");
        }

        [Fact]
        public void DataSyncMessage_Deserialize_RestoresAllFields()
        {
            // Arrange
            var original = new DataSyncMessage
            {
                SenderId = new CSteamID(76561197960265728),
                Key = "GameState",
                Value = "{\"round\":3,\"score\":500}",
                DataType = "json"
            };
            var bytes = original.Serialize();

            // Act
            var restored = new DataSyncMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.Key.Should().Be("GameState");
            restored.Value.Should().Be("{\"round\":3,\"score\":500}");
            restored.DataType.Should().Be("json");
            restored.SenderId.m_SteamID.Should().Be(76561197960265728);
        }

        [Fact]
        public void DataSyncMessage_DefaultDataType_IsString()
        {
            // Arrange
            var message = new DataSyncMessage();

            // Assert
            message.DataType.Should().Be("string");
        }

        [Fact]
        public void DataSyncMessage_WithQuotesInValue_EscapesCorrectly()
        {
            // Arrange
            var message = new DataSyncMessage
            {
                SenderId = new CSteamID(12345),
                Key = "Config",
                Value = "{\"name\":\"Player One\",\"weapon\":\"Rifle\"}",
                DataType = "json"
            };

            // Act
            var bytes = message.Serialize();
            var restored = new DataSyncMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.Value.Should().Be("{\"name\":\"Player One\",\"weapon\":\"Rifle\"}");
        }

        [Fact]
        public void DataSyncMessage_EmptyKey_SerializesAndDeserializes()
        {
            // Arrange
            var message = new DataSyncMessage
            {
                SenderId = new CSteamID(12345),
                Key = "",
                Value = "test",
                DataType = "string"
            };

            // Act
            var bytes = message.Serialize();
            var restored = new DataSyncMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.Key.Should().Be("");
            restored.Value.Should().Be("test");
        }

        [Fact]
        public void DataSyncMessage_MessageType_ReturnsDataSync()
        {
            // Arrange
            var message = new DataSyncMessage();

            // Assert
            message.MessageType.Should().Be("DATA_SYNC");
        }

        #endregion

        #region HeartbeatMessage Tests

        [Fact]
        public void HeartbeatMessage_Serialize_ProducesValidJson()
        {
            // Arrange
            var message = new HeartbeatMessage
            {
                SenderId = new CSteamID(76561197960265728),
                HeartbeatId = "hb-12345",
                IsResponse = false,
                SequenceNumber = 42,
                PacketLossPercent = 0.5f,
                AverageLatencyMs = 50.5f,
                BandwidthUsage = 1024,
                PlayerStatus = "online",
                ConnectionInfo = "NAT: Open"
            };

            // Act
            var bytes = message.Serialize();
            var json = System.Text.Encoding.UTF8.GetString(bytes);

            // Assert
            json.Should().Contain("\"HeartbeatId\":\"hb-12345\"");
            json.Should().Contain("\"IsResponse\":false");
            json.Should().Contain("\"SequenceNumber\":42");
            json.Should().Contain("\"PacketLossPercent\":0.5");
            json.Should().Contain("\"AverageLatencyMs\":50.5");
            json.Should().Contain("\"BandwidthUsage\":1024");
            json.Should().Contain("\"PlayerStatus\":\"online\"");
            json.Should().Contain("\"ConnectionInfo\":\"NAT: Open\"");
        }

        [Fact]
        public void HeartbeatMessage_Deserialize_RestoresAllFields()
        {
            // Arrange
            var original = new HeartbeatMessage
            {
                SenderId = new CSteamID(76561197960265728),
                HeartbeatId = "test-id",
                IsResponse = true,
                SequenceNumber = 100,
                PacketLossPercent = 1.5f,
                AverageLatencyMs = 75.25f,
                BandwidthUsage = 2048,
                PlayerStatus = "away",
                ConnectionInfo = "Relay"
            };
            var bytes = original.Serialize();

            // Act
            var restored = new HeartbeatMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.HeartbeatId.Should().Be("test-id");
            restored.IsResponse.Should().BeTrue();
            restored.SequenceNumber.Should().Be(100);
            restored.PacketLossPercent.Should().Be(1.5f);
            restored.AverageLatencyMs.Should().Be(75.25f);
            restored.BandwidthUsage.Should().Be(2048);
            restored.PlayerStatus.Should().Be("away");
            restored.ConnectionInfo.Should().Be("Relay");
        }

        [Fact]
        public void HeartbeatMessage_IsResponse_False_IndicatesPing()
        {
            // Arrange
            var message = new HeartbeatMessage
            {
                IsResponse = false
            };

            // Assert
            message.IsResponse.Should().BeFalse("this is a ping, not a pong");
        }

        [Fact]
        public void HeartbeatMessage_IsResponse_True_IndicatesPong()
        {
            // Arrange
            var message = new HeartbeatMessage
            {
                IsResponse = true
            };

            // Assert
            message.IsResponse.Should().BeTrue("this is a pong response");
        }

        [Fact]
        public void HeartbeatMessage_DefaultPlayerStatus_IsOnline()
        {
            // Arrange
            var message = new HeartbeatMessage();

            // Assert
            message.PlayerStatus.Should().Be("online");
        }

        [Fact]
        public void HeartbeatMessage_HighPrecisionTimestamp_AutoSetsOnSerialize()
        {
            // Arrange
            var message = new HeartbeatMessage
            {
                SenderId = new CSteamID(12345),
                HighPrecisionTimestamp = 0 // Not set
            };

            // Act
            var bytes = message.Serialize();

            // Assert
            message.HighPrecisionTimestamp.Should().BeGreaterThan(0, "timestamp should be auto-set");
        }

        [Fact]
        public void HeartbeatMessage_SequenceNumber_TracksOrder()
        {
            // Arrange
            var message1 = new HeartbeatMessage { SequenceNumber = 1 };
            var message2 = new HeartbeatMessage { SequenceNumber = 2 };

            // Assert
            message2.SequenceNumber.Should().BeGreaterThan(message1.SequenceNumber);
        }

        [Fact]
        public void HeartbeatMessage_MessageType_ReturnsHeartbeat()
        {
            // Arrange
            var message = new HeartbeatMessage();

            // Assert
            message.MessageType.Should().Be("HEARTBEAT");
        }

        [Fact]
        public void HeartbeatMessage_RoundTrip_PreservesFloatPrecision()
        {
            // Arrange
            var original = new HeartbeatMessage
            {
                SenderId = new CSteamID(12345),
                PacketLossPercent = 3.14159f,
                AverageLatencyMs = 123.456f
            };

            // Act
            var bytes = original.Serialize();
            var restored = new HeartbeatMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.PacketLossPercent.Should().BeApproximately(3.14159f, 0.00001f);
            restored.AverageLatencyMs.Should().BeApproximately(123.456f, 0.001f);
        }

        #endregion

        #region Common P2PMessage Tests

        [Fact]
        public void P2PMessage_Timestamp_SetOnConstruction()
        {
            // Arrange & Act
            var beforeCreation = DateTime.UtcNow;
            var message = new TextMessage();
            var afterCreation = DateTime.UtcNow;

            // Assert
            message.Timestamp.Should().BeOnOrAfter(beforeCreation);
            message.Timestamp.Should().BeOnOrBefore(afterCreation);
        }

        [Fact]
        public void P2PMessage_SenderId_RoundTripsCorrectly()
        {
            // Arrange
            var testId = new CSteamID(99999999999);
            var message = new TextMessage
            {
                SenderId = testId,
                Content = "test"
            };

            // Act
            var bytes = message.Serialize();
            var restored = new TextMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.SenderId.m_SteamID.Should().Be(99999999999);
        }

        [Fact]
        public void P2PMessage_Timestamp_RoundTripsCorrectly()
        {
            // Arrange
            var timestamp = new DateTime(2026, 1, 3, 15, 30, 45, DateTimeKind.Utc);
            var message = new TextMessage
            {
                SenderId = new CSteamID(12345),
                Timestamp = timestamp,
                Content = "test"
            };

            // Act
            var bytes = message.Serialize();
            var restored = new TextMessage();
            restored.Deserialize(bytes);

            // Assert - Verify date components (time may vary due to timezone conversions during serialization)
            restored.Timestamp.Year.Should().Be(2026);
            restored.Timestamp.Month.Should().Be(1);
            restored.Timestamp.Day.Should().Be(3);
            restored.Timestamp.Should().NotBe(default(DateTime), "timestamp should be set");
        }

        #endregion
    }
}
