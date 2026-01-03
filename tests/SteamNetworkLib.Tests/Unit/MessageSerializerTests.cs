using System;
using System.Text;
using FluentAssertions;
using SteamNetworkLib.Exceptions;
using SteamNetworkLib.Models;
using SteamNetworkLib.Utilities;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    /// <summary>
    /// Unit tests for MessageSerializer utility class.
    /// Tests serialization/deserialization of P2P messages.
    /// </summary>
    public class MessageSerializerTests
    {
        [Fact]
        public void SerializeMessage_TextMessage_ProducesValidBytes()
        {
            // Arrange
            var message = new TextMessage { Content = "Hello, World!" };

            // Act
            var serialized = MessageSerializer.SerializeMessage(message);

            // Assert
            serialized.Should().NotBeNull();
            serialized.Length.Should().BeGreaterThan(0);
            
            // Should start with header "SNLM"
            var header = Encoding.UTF8.GetString(serialized, 0, 4);
            header.Should().Be("SNLM");
        }

        [Fact]
        public void SerializeMessage_DataSyncMessage_ProducesValidBytes()
        {
            // Arrange
            var message = new DataSyncMessage
            {
                Key = "TestKey",
                Value = "TestValue",
                DataType = "string"
            };

            // Act
            var serialized = MessageSerializer.SerializeMessage(message);

            // Assert
            serialized.Should().NotBeNull();
            serialized.Length.Should().BeGreaterThan(0);
            
            // Should start with header "SNLM"
            var header = Encoding.UTF8.GetString(serialized, 0, 4);
            header.Should().Be("SNLM");
        }

        [Fact]
        public void SerializeMessage_HeartbeatMessage_ProducesValidBytes()
        {
            // Arrange
            var message = new HeartbeatMessage
            {
                SequenceNumber = 42,
                IsResponse = true
            };

            // Act
            var serialized = MessageSerializer.SerializeMessage(message);

            // Assert
            serialized.Should().NotBeNull();
            var header = Encoding.UTF8.GetString(serialized, 0, 4);
            header.Should().Be("SNLM");
        }

        [Fact]
        public void DeserializeMessage_ValidData_ReturnsTypeAndData()
        {
            // Arrange
            var message = new TextMessage { Content = "Test content" };
            var serialized = MessageSerializer.SerializeMessage(message);

            // Act
            var (messageType, messageData) = MessageSerializer.DeserializeMessage(serialized);

            // Assert
            messageType.Should().Be("TEXT");
            messageData.Should().NotBeNull();
            messageData.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void DeserializeMessage_DataTooShort_ThrowsP2PException()
        {
            // Arrange
            var shortData = new byte[] { 0x53, 0x4E, 0x4C }; // Only 3 bytes

            // Act & Assert
            Action act = () => MessageSerializer.DeserializeMessage(shortData);
            act.Should().Throw<P2PException>()
               .WithMessage("*too short*");
        }

        [Fact]
        public void DeserializeMessage_InvalidHeader_ThrowsP2PException()
        {
            // Arrange
            var invalidData = new byte[] { 0x58, 0x58, 0x58, 0x58, 0x04, 0x54, 0x45, 0x53, 0x54 }; // "XXXX" header

            // Act & Assert
            Action act = () => MessageSerializer.DeserializeMessage(invalidData);
            act.Should().Throw<P2PException>()
               .WithMessage("*Invalid message header*");
        }

        [Fact]
        public void CreateMessage_ValidTextMessage_ReturnsCorrectType()
        {
            // Arrange
            var originalMessage = new TextMessage { Content = "Hello, Test!" };
            var serialized = MessageSerializer.SerializeMessage(originalMessage);

            // Act
            var deserialized = MessageSerializer.CreateMessage<TextMessage>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.MessageType.Should().Be("TEXT");
            deserialized.Content.Should().Be("Hello, Test!");
        }

        [Fact]
        public void CreateMessage_TypeMismatch_ThrowsP2PException()
        {
            // Arrange
            var textMessage = new TextMessage { Content = "Test" };
            var serialized = MessageSerializer.SerializeMessage(textMessage);

            // Act & Assert - Try to deserialize as DataSyncMessage
            Action act = () => MessageSerializer.CreateMessage<DataSyncMessage>(serialized);
            act.Should().Throw<P2PException>()
               .WithMessage("*type mismatch*");
        }

        [Fact]
        public void IsValidMessage_ValidData_ReturnsTrue()
        {
            // Arrange
            var message = new TextMessage { Content = "Valid message" };
            var serialized = MessageSerializer.SerializeMessage(message);

            // Act
            var isValid = MessageSerializer.IsValidMessage(serialized);

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public void IsValidMessage_InvalidHeader_ReturnsFalse()
        {
            // Arrange - data with wrong header
            var invalidData = new byte[] { 0x58, 0x58, 0x58, 0x58, 0x04, 0x54, 0x45, 0x53, 0x54, 0x00 };

            // Act
            var isValid = MessageSerializer.IsValidMessage(invalidData);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void IsValidMessage_TooShort_ReturnsFalse()
        {
            // Arrange
            var shortData = new byte[] { 0x53, 0x4E, 0x4C }; // Only 3 bytes

            // Act
            var isValid = MessageSerializer.IsValidMessage(shortData);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void IsValidMessage_EmptyData_ReturnsFalse()
        {
            // Arrange
            var emptyData = Array.Empty<byte>();

            // Act
            var isValid = MessageSerializer.IsValidMessage(emptyData);

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public void GetMessageType_ValidData_ReturnsType()
        {
            // Arrange
            var message = new TextMessage { Content = "Test" };
            var serialized = MessageSerializer.SerializeMessage(message);

            // Act
            var messageType = MessageSerializer.GetMessageType(serialized);

            // Assert
            messageType.Should().Be("TEXT");
        }

        [Fact]
        public void GetMessageType_InvalidData_ReturnsNull()
        {
            // Arrange
            var invalidData = new byte[] { 0x00, 0x01, 0x02 };

            // Act
            var messageType = MessageSerializer.GetMessageType(invalidData);

            // Assert
            messageType.Should().BeNull();
        }

        [Fact]
        public void RoundTrip_TextMessage_PreservesContent()
        {
            // Arrange
            var original = new TextMessage { Content = "Round trip test with special chars: !@#$%^&*()" };

            // Act
            var serialized = MessageSerializer.SerializeMessage(original);
            var deserialized = MessageSerializer.CreateMessage<TextMessage>(serialized);

            // Assert
            deserialized.Content.Should().Be(original.Content);
        }

        [Fact]
        public void RoundTrip_DataSyncMessage_PreservesAllFields()
        {
            // Arrange
            var original = new DataSyncMessage
            {
                Key = "player_score",
                Value = "12345",
                DataType = "int"
            };

            // Act
            var serialized = MessageSerializer.SerializeMessage(original);
            var deserialized = MessageSerializer.CreateMessage<DataSyncMessage>(serialized);

            // Assert
            deserialized.Key.Should().Be(original.Key);
            deserialized.Value.Should().Be(original.Value);
            deserialized.DataType.Should().Be(original.DataType);
        }

        [Fact]
        public void RoundTrip_HeartbeatMessage_PreservesAllFields()
        {
            // Arrange
            var original = new HeartbeatMessage
            {
                SequenceNumber = 100,
                IsResponse = true,
                PacketLossPercent = 2.5f,
                AverageLatencyMs = 45.0f
            };

            // Act
            var serialized = MessageSerializer.SerializeMessage(original);
            var deserialized = MessageSerializer.CreateMessage<HeartbeatMessage>(serialized);

            // Assert
            deserialized.SequenceNumber.Should().Be(original.SequenceNumber);
            deserialized.IsResponse.Should().Be(original.IsResponse);
            deserialized.PacketLossPercent.Should().BeApproximately(original.PacketLossPercent, 0.01f);
            deserialized.AverageLatencyMs.Should().BeApproximately(original.AverageLatencyMs, 0.01f);
        }
    }
}
