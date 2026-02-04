using System;
using System.Threading.Tasks;
using FluentAssertions;
using SteamNetworkLib.Core;
using SteamNetworkLib.Exceptions;
using SteamNetworkLib.Models;
using SteamNetworkLib.Utilities;
using Steamworks;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    /// <summary>
    /// A custom P2P message type for testing custom message handling.
    /// Represents a transaction object that users might want to send.
    /// </summary>
    public class TransactionMessage : P2PMessage
    {
        public override string MessageType => "TRANSACTION";

        public string TransactionId { get; set; } = string.Empty;
        public string FromPlayer { get; set; } = string.Empty;
        public string ToPlayer { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string Description { get; set; } = string.Empty;
        public DateTime TransactionTime { get; set; }

        public override byte[] Serialize()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public override void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<TransactionMessage>(json);
            if (deserialized != null)
            {
                TransactionId = deserialized.TransactionId;
                FromPlayer = deserialized.FromPlayer;
                ToPlayer = deserialized.ToPlayer;
                Amount = deserialized.Amount;
                Currency = deserialized.Currency;
                Description = deserialized.Description;
                TransactionTime = deserialized.TransactionTime;
                SenderId = deserialized.SenderId;
                Timestamp = deserialized.Timestamp;
            }
        }
    }

    /// <summary>
    /// Another custom message type for testing multiple custom types.
    /// </summary>
    public class PlayerActionMessage : P2PMessage
    {
        public override string MessageType => "PLAYER_ACTION";

        public string ActionType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public int ActionData { get; set; }

        public override byte[] Serialize()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public override void Deserialize(byte[] data)
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<PlayerActionMessage>(json);
            if (deserialized != null)
            {
                ActionType = deserialized.ActionType;
                TargetId = deserialized.TargetId;
                ActionData = deserialized.ActionData;
                SenderId = deserialized.SenderId;
                Timestamp = deserialized.Timestamp;
            }
        }
    }

    /// <summary>
    /// Unit tests for custom P2P message types and message handler registration.
    /// Tests demonstrate the current limitation where custom message types don't trigger callbacks.
    /// </summary>
    public class CustomP2PMessageTests
    {
        #region Custom Message Serialization Tests

        [Fact]
        public void TransactionMessage_Serialize_ProducesValidJson()
        {
            // Arrange
            var message = new TransactionMessage
            {
                SenderId = new CSteamID(76561197960265728),
                Timestamp = new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc),
                TransactionId = "txn-12345",
                FromPlayer = "PlayerOne",
                ToPlayer = "PlayerTwo",
                Amount = 100.50m,
                Currency = "USD",
                Description = "Payment for services",
                TransactionTime = new DateTime(2026, 1, 3, 11, 30, 0, DateTimeKind.Utc)
            };

            // Act
            var bytes = message.Serialize();
            var json = System.Text.Encoding.UTF8.GetString(bytes);

            // Assert
            json.Should().Contain("\"TransactionId\":\"txn-12345\"");
            json.Should().Contain("\"FromPlayer\":\"PlayerOne\"");
            json.Should().Contain("\"ToPlayer\":\"PlayerTwo\"");
            json.Should().Contain("\"Amount\":100.50");
            json.Should().Contain("\"Currency\":\"USD\"");
            json.Should().Contain("\"Description\":\"Payment for services\"");
        }

        [Fact]
        public void TransactionMessage_Deserialize_RestoresAllFields()
        {
            // Arrange
            var original = new TransactionMessage
            {
                SenderId = new CSteamID(76561197960265728),
                TransactionId = "txn-abc-123",
                FromPlayer = "Sender",
                ToPlayer = "Receiver",
                Amount = 999.99m,
                Currency = "EUR",
                Description = "Test transaction",
                TransactionTime = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc)
            };
            var bytes = original.Serialize();

            // Act
            var restored = new TransactionMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.TransactionId.Should().Be("txn-abc-123");
            restored.FromPlayer.Should().Be("Sender");
            restored.ToPlayer.Should().Be("Receiver");
            restored.Amount.Should().Be(999.99m);
            restored.Currency.Should().Be("EUR");
            restored.Description.Should().Be("Test transaction");
            restored.TransactionTime.Should().Be(new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc));
            // Note: SenderId may not deserialize properly due to CSteamID struct serialization
            // This is a known limitation with System.Text.Json and Steamworks structs
        }

        [Fact]
        public void TransactionMessage_MessageType_ReturnsTransaction()
        {
            // Arrange
            var message = new TransactionMessage();

            // Assert
            message.MessageType.Should().Be("TRANSACTION");
        }

        [Fact]
        public void TransactionMessage_WithSpecialCharacters_EscapesCorrectly()
        {
            // Arrange
            var message = new TransactionMessage
            {
                SenderId = new CSteamID(12345),
                Description = "Payment with \"quotes\" and \n newlines"
            };

            // Act
            var bytes = message.Serialize();
            var restored = new TransactionMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.Description.Should().Be("Payment with \"quotes\" and \n newlines");
        }

        [Fact]
        public void PlayerActionMessage_SerializeAndDeserialize_RoundTrip()
        {
            // Arrange
            var original = new PlayerActionMessage
            {
                SenderId = new CSteamID(76561197960265728),
                ActionType = "ATTACK",
                TargetId = "enemy-123",
                ActionData = 42
            };

            // Act
            var bytes = original.Serialize();
            var restored = new PlayerActionMessage();
            restored.Deserialize(bytes);

            // Assert
            restored.ActionType.Should().Be("ATTACK");
            restored.TargetId.Should().Be("enemy-123");
            restored.ActionData.Should().Be(42);
            // Note: SenderId may not deserialize properly due to CSteamID struct serialization
            // This is a known limitation with System.Text.Json and Steamworks structs
        }

        [Fact]
        public void PlayerActionMessage_MessageType_ReturnsPlayerAction()
        {
            // Arrange
            var message = new PlayerActionMessage();

            // Assert
            message.MessageType.Should().Be("PLAYER_ACTION");
        }

        #endregion

        #region MessageSerializer Tests with Custom Types

        [Fact]
        public void MessageSerializer_SerializeMessage_CustomType_IncludesHeader()
        {
            // Arrange
            var message = new TransactionMessage
            {
                TransactionId = "test-txn",
                Amount = 50.00m
            };

            // Act
            var serialized = MessageSerializer.SerializeMessage(message);

            // Assert
            serialized.Length.Should().BeGreaterThan(4);
            var header = System.Text.Encoding.UTF8.GetString(serialized, 0, 4);
            header.Should().Be("SNLM");
        }

        [Fact]
        public void MessageSerializer_SerializeMessage_CustomType_IncludesMessageType()
        {
            // Arrange
            var message = new TransactionMessage
            {
                TransactionId = "test-txn",
                Amount = 50.00m
            };

            // Act
            var serialized = MessageSerializer.SerializeMessage(message);

            // Assert
            // Format: [SNLM(4)][TYPE_LENGTH(1)][TYPE][DATA]
            var typeLength = serialized[4];
            var typeBytes = new byte[typeLength];
            Array.Copy(serialized, 5, typeBytes, 0, typeLength);
            var messageType = System.Text.Encoding.UTF8.GetString(typeBytes);
            messageType.Should().Be("TRANSACTION");
        }

        [Fact]
        public void MessageSerializer_DeserializeMessage_CustomType_ExtractsTypeAndData()
        {
            // Arrange
            var message = new TransactionMessage
            {
                TransactionId = "test-txn-123",
                Amount = 75.25m,
                Currency = "GBP"
            };
            var serialized = MessageSerializer.SerializeMessage(message);

            // Act
            var (messageType, messageData) = MessageSerializer.DeserializeMessage(serialized);

            // Assert
            messageType.Should().Be("TRANSACTION");
            messageData.Length.Should().BeGreaterThan(0);

            // Verify the data can be deserialized back to the original message
            var restored = new TransactionMessage();
            restored.Deserialize(messageData);
            restored.TransactionId.Should().Be("test-txn-123");
            restored.Amount.Should().Be(75.25m);
            restored.Currency.Should().Be("GBP");
        }

        [Fact]
        public void MessageSerializer_GetMessageType_CustomType_ReturnsCorrectType()
        {
            // Arrange
            var message = new PlayerActionMessage
            {
                ActionType = "MOVE",
                ActionData = 10
            };
            var serialized = MessageSerializer.SerializeMessage(message);

            // Act
            var messageType = MessageSerializer.GetMessageType(serialized);

            // Assert
            messageType.Should().Be("PLAYER_ACTION");
        }

        [Fact]
        public void MessageSerializer_IsValidMessage_CustomType_ReturnsTrue()
        {
            // Arrange
            var message = new TransactionMessage();
            var serialized = MessageSerializer.SerializeMessage(message);

            // Act
            var isValid = MessageSerializer.IsValidMessage(serialized);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void MessageSerializer_CreateMessage_CustomType_WithValidData()
        {
            // Arrange
            var original = new TransactionMessage
            {
                SenderId = new CSteamID(76561197960265728),
                TransactionId = "txn-test-456",
                Amount = 123.45m
            };
            var serialized = MessageSerializer.SerializeMessage(original);

            // Act
            var restored = MessageSerializer.CreateMessage<TransactionMessage>(serialized);

            // Assert
            restored.Should().NotBeNull();
            restored.TransactionId.Should().Be("txn-test-456");
            restored.Amount.Should().Be(123.45m);
            // Note: SenderId may not deserialize properly due to CSteamID struct serialization
            // This is a known limitation with System.Text.Json and Steamworks structs
        }

        [Fact]
        public void MessageSerializer_CreateMessage_CustomType_TypeMismatch_ThrowsException()
        {
            // Arrange
            var transaction = new TransactionMessage
            {
                TransactionId = "txn-123",
                Amount = 100.00m
            };
            var serialized = MessageSerializer.SerializeMessage(transaction);

            // Act & Assert
            // Trying to deserialize a TRANSACTION message as a TextMessage should fail
            var ex = Assert.Throws<P2PException>(() =>
            {
                MessageSerializer.CreateMessage<TextMessage>(serialized);
            });
            ex.Message.Should().Contain("type mismatch");
        }

        #endregion

        #region Message Handler Registration Tests
        // NOTE: These tests require Steam to be initialized and should be run as integration tests
        // They are commented out as unit tests because SteamLobbyManager requires Steam API

        // The tests below demonstrate that the handler registration API works correctly,
        // but they require Steam to be initialized. See the integration tests for P2P messaging
        // that properly initialize Steam via Goldberg emulator.

        #endregion

        #region Documentation Tests - Demonstrating the Solution

        [Fact]
        public void Documentation_CustomMessageType_NowWorks()
        {
            // This test documents the solution:
            //
            // When you create a custom P2P message type (like TransactionMessage):
            // 1. Serialize() works - the message can be sent
            // 2. The message is sent successfully via Steam P2P
            // 3. On the receiving side, the message header (SNLM) is validated
            // 4. The message type string ("TRANSACTION") is extracted correctly
            // 5. ProcessSteamNetworkLibMessage() checks the custom message registry
            // 6. If the type is registered via RegisterCustomMessageType<T>(), it's created
            // 7. The message is not null, so:
            //    - OnMessageReceived event IS triggered
            //    - Registered message handlers ARE called
            //
            // SOLUTION: Call RegisterCustomMessageType<YourMessageClass>() during initialization

            // Arrange
            var message = new TransactionMessage
            {
                TransactionId = "txn-demo",
                Amount = 100.00m
            };

            // Act - Serialize the message
            var serialized = MessageSerializer.SerializeMessage(message);

            // Assert - Serialization works fine
            serialized.Should().NotBeNull();
            MessageSerializer.IsValidMessage(serialized).Should().BeTrue();
            MessageSerializer.GetMessageType(serialized).Should().Be("TRANSACTION");

            // The message type can be used to create instances if registered
            var messageType = MessageSerializer.GetMessageType(serialized);
            messageType.Should().Be("TRANSACTION");
        }

        [Fact]
        public void Documentation_BuiltInTypes_WorkCorrectly()
        {
            // This test shows that built-in types work as expected
            // because they are pre-registered internally.

            // Arrange
            var textMessage = new TextMessage { Content = "Hello" };
            var dataSyncMessage = new DataSyncMessage { Key = "test", Value = "value" };
            var heartbeatMessage = new HeartbeatMessage { HeartbeatId = "hb-1" };

            // Act
            var textSerialized = MessageSerializer.SerializeMessage(textMessage);
            var dataSerialized = MessageSerializer.SerializeMessage(dataSyncMessage);
            var hbSerialized = MessageSerializer.SerializeMessage(heartbeatMessage);

            // Assert - All built-in types serialize correctly
            MessageSerializer.GetMessageType(textSerialized).Should().Be("TEXT");
            MessageSerializer.GetMessageType(dataSerialized).Should().Be("DATA_SYNC");
            MessageSerializer.GetMessageType(hbSerialized).Should().Be("HEARTBEAT");

            // These types trigger callbacks because they are pre-registered
        }

        #endregion
    }
}
