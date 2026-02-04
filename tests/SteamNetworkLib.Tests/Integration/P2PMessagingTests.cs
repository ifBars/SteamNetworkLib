using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SteamNetworkLib.Core;
using SteamNetworkLib.Models;
using SteamNetworkLib.Tests.TestUtilities;
using SteamNetworkLib.Utilities;
using Steamworks;
using Xunit;
using Xunit.Abstractions;

namespace SteamNetworkLib.Tests.Integration
{
    /// <summary>
    /// Integration tests for P2P messaging functionality using Goldberg Steam Emulator.
    /// Tests message sending, receiving, and handler registration.
    /// </summary>    
    [Collection("Steam Integration Tests")]
    public class P2PMessagingTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly SteamCollectionFixture _fixture;
        private bool _disposed;

        public P2PMessagingTests(SteamCollectionFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            
            // Leave any existing lobby before each test
            _fixture.Fixture.LeaveLobby();
        }

        #region Built-in Message Type Tests

        [Fact]
        public async Task SendMessageToPlayerAsync_TextMessage_SendsSuccessfully()
        {
            // Arrange
            _output.WriteLine("=== Test: Send TextMessage via P2P ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var message = new TextMessage
            {
                Content = "Hello from P2P test!"
            };

            // Act - Send to ourselves (since we don't have another client in single-process tests)
            var result = await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            // Assert
            result.Should().BeTrue("message should be sent successfully");
            _output.WriteLine("✓ TextMessage sent successfully");
        }

        [Fact]
        public async Task SendMessageToPlayerAsync_DataSyncMessage_SendsSuccessfully()
        {
            // Arrange
            _output.WriteLine("=== Test: Send DataSyncMessage via P2P ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var message = new DataSyncMessage
            {
                Key = "TestKey",
                Value = "TestValue",
                DataType = "string"
            };

            // Act
            var result = await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            // Assert
            result.Should().BeTrue("message should be sent successfully");
            _output.WriteLine("✓ DataSyncMessage sent successfully");
        }

        [Fact]
        public async Task SendMessageToPlayerAsync_HeartbeatMessage_SendsSuccessfully()
        {
            // Arrange
            _output.WriteLine("=== Test: Send HeartbeatMessage via P2P ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var message = new HeartbeatMessage
            {
                HeartbeatId = $"hb-{Guid.NewGuid()}",
                IsResponse = false,
                SequenceNumber = 1
            };

            // Act
            var result = await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            // Assert
            result.Should().BeTrue("message should be sent successfully");
            _output.WriteLine("✓ HeartbeatMessage sent successfully");
        }

        [Fact]
        public async Task OnP2PMessageReceived_TextMessage_TriggersEvent()
        {
            // Arrange
            _output.WriteLine("=== Test: OnP2PMessageReceived event for TextMessage ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var eventReceived = false;
            TextMessage? receivedMessage = null;
            var tcs = new TaskCompletionSource<bool>();

            // Subscribe to the event
            _fixture.Fixture.Client!.OnP2PMessageReceived += (sender, args) =>
            {
                _output.WriteLine($"Event received: {args.Message.MessageType}");
                if (args.Message is TextMessage textMsg)
                {
                    eventReceived = true;
                    receivedMessage = textMsg;
                    tcs.TrySetResult(true);
                }
            };

            var message = new TextMessage
            {
                Content = "Test event message"
            };

            // Act
            await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            // Wait for message to be processed (with timeout)
            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            await Task.Delay(100); // Give time for event to fire

            // Assert
            eventReceived.Should().BeTrue("OnP2PMessageReceived event should fire for TextMessage");
            receivedMessage.Should().NotBeNull();
            receivedMessage!.Content.Should().Be("Test event message");
            _output.WriteLine("✓ OnP2PMessageReceived event fired correctly for TextMessage");
        }

        [Fact]
        public async Task OnP2PMessageReceived_DataSyncMessage_TriggersEvent()
        {
            // Arrange
            _output.WriteLine("=== Test: OnP2PMessageReceived event for DataSyncMessage ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var eventReceived = false;
            DataSyncMessage? receivedMessage = null;
            var tcs = new TaskCompletionSource<bool>();

            _fixture.Fixture.Client!.OnP2PMessageReceived += (sender, args) =>
            {
                _output.WriteLine($"Event received: {args.Message.MessageType}");
                if (args.Message is DataSyncMessage dataMsg)
                {
                    eventReceived = true;
                    receivedMessage = dataMsg;
                    tcs.TrySetResult(true);
                }
            };

            var message = new DataSyncMessage
            {
                Key = "PlayerScore",
                Value = "1000",
                DataType = "integer"
            };

            // Act
            await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            await Task.Delay(100);

            // Assert
            eventReceived.Should().BeTrue("OnP2PMessageReceived event should fire for DataSyncMessage");
            receivedMessage.Should().NotBeNull();
            receivedMessage!.Key.Should().Be("PlayerScore");
            receivedMessage.Value.Should().Be("1000");
            _output.WriteLine("✓ OnP2PMessageReceived event fired correctly for DataSyncMessage");
        }

        [Fact]
        public async Task RegisterMessageHandler_TextMessage_CallsHandler()
        {
            // Arrange
            _output.WriteLine("=== Test: RegisterMessageHandler for TextMessage ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var handlerCalled = false;
            TextMessage? receivedMessage = null;
            var tcs = new TaskCompletionSource<bool>();

            _fixture.Fixture.Client!.RegisterMessageHandler<TextMessage>((message, senderId) =>
            {
                _output.WriteLine($"Handler called for TextMessage from {senderId.m_SteamID}");
                handlerCalled = true;
                receivedMessage = message;
                tcs.TrySetResult(true);
            });

            var message = new TextMessage
            {
                Content = "Handler test message"
            };

            // Act
            await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            await Task.Delay(100);

            // Assert
            handlerCalled.Should().BeTrue("registered handler should be called for TextMessage");
            receivedMessage.Should().NotBeNull();
            receivedMessage!.Content.Should().Be("Handler test message");
            _output.WriteLine("✓ RegisterMessageHandler worked correctly for TextMessage");
        }

        [Fact]
        public async Task RegisterMessageHandler_DataSyncMessage_CallsHandler()
        {
            // Arrange
            _output.WriteLine("=== Test: RegisterMessageHandler for DataSyncMessage ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var handlerCalled = false;
            DataSyncMessage? receivedMessage = null;
            var tcs = new TaskCompletionSource<bool>();

            _fixture.Fixture.Client!.RegisterMessageHandler<DataSyncMessage>((message, senderId) =>
            {
                _output.WriteLine($"Handler called for DataSyncMessage");
                handlerCalled = true;
                receivedMessage = message;
                tcs.TrySetResult(true);
            });

            var message = new DataSyncMessage
            {
                Key = "GameState",
                Value = "Playing",
                DataType = "string"
            };

            // Act
            await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            await Task.Delay(100);

            // Assert
            handlerCalled.Should().BeTrue("registered handler should be called for DataSyncMessage");
            receivedMessage.Should().NotBeNull();
            receivedMessage!.Key.Should().Be("GameState");
            _output.WriteLine("✓ RegisterMessageHandler worked correctly for DataSyncMessage");
        }

        [Fact]
        public async Task RegisterMessageHandler_MultipleHandlers_AllCalled()
        {
            // Arrange
            _output.WriteLine("=== Test: Multiple handlers for same message type ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var handler1Called = false;
            var handler2Called = false;
            var tcs = new TaskCompletionSource<bool>();
            var handlersCompleted = 0;

            _fixture.Fixture.Client!.RegisterMessageHandler<TextMessage>((message, senderId) =>
            {
                _output.WriteLine("Handler 1 called");
                handler1Called = true;
                Interlocked.Increment(ref handlersCompleted);
                if (handlersCompleted >= 2) tcs.TrySetResult(true);
            });

            _fixture.Fixture.Client!.RegisterMessageHandler<TextMessage>((message, senderId) =>
            {
                _output.WriteLine("Handler 2 called");
                handler2Called = true;
                Interlocked.Increment(ref handlersCompleted);
                if (handlersCompleted >= 2) tcs.TrySetResult(true);
            });

            var message = new TextMessage
            {
                Content = "Multiple handlers test"
            };

            // Act
            await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            await Task.Delay(100);

            // Assert
            handler1Called.Should().BeTrue("first handler should be called");
            handler2Called.Should().BeTrue("second handler should be called");
            _output.WriteLine("✓ Multiple handlers were both called");
        }

        #endregion

        #region Custom Message Type Tests

        [Fact]
        public async Task SendMessageToPlayerAsync_CustomTransactionMessage_SendsSuccessfully()
        {
            // Arrange
            _output.WriteLine("=== Test: Send custom TransactionMessage via P2P ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var message = new CustomTransactionMessage
            {
                TransactionId = "txn-test-123",
                FromPlayer = "PlayerA",
                ToPlayer = "PlayerB",
                Amount = 50.00m,
                Currency = "USD",
                Description = "Test transaction"
            };

            // Act
            var result = await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            // Assert - Message sends successfully
            result.Should().BeTrue("custom message should be sent successfully");
            _output.WriteLine("✓ Custom TransactionMessage sent successfully");
        }

        [Fact]
        public async Task OnP2PMessageReceived_CustomMessage_NowTriggersEvent()
        {
            // Arrange
            _output.WriteLine("=== Test: OnP2PMessageReceived event for custom message (FIXED) ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var eventReceived = false;
            var tcs = new TaskCompletionSource<bool>();

            _fixture.Fixture.Client!.OnP2PMessageReceived += (sender, args) =>
            {
                _output.WriteLine($"Event received: {args.Message.MessageType}");
                if (args.Message.MessageType == "TRANSACTION")
                {
                    eventReceived = true;
                    tcs.TrySetResult(true);
                }
            };

            var message = new CustomTransactionMessage
            {
                TransactionId = "txn-event-test",
                Amount = 100.00m
            };

            // Act
            await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            await Task.Delay(100);

            // Assert - Event NOW fires for custom types when a handler is registered
            eventReceived.Should().BeTrue("OnP2PMessageReceived should fire for custom types when RegisterMessageHandler is called");
            _output.WriteLine("✓ FIXED: OnP2PMessageReceived now fires for custom message types");
        }

        [Fact]
        public async Task RegisterMessageHandler_CustomMessage_NowCallsHandler()
        {
            // Arrange
            _output.WriteLine("=== Test: RegisterMessageHandler for custom message (FIXED) ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var handlerCalled = false;
            CustomTransactionMessage? receivedMessage = null;
            var tcs = new TaskCompletionSource<bool>();

            _fixture.Fixture.Client!.RegisterMessageHandler<CustomTransactionMessage>((message, senderId) =>
            {
                _output.WriteLine($"Handler called for TRANSACTION message: {message.TransactionId}");
                handlerCalled = true;
                receivedMessage = message;
                tcs.TrySetResult(true);
            });

            var message = new CustomTransactionMessage
            {
                TransactionId = "txn-handler-test",
                Amount = 75.00m
            };

            // Act
            await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                message
            );

            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            await Task.Delay(100);

            // Assert - Handler gets called and automatically registers the custom type
            handlerCalled.Should().BeTrue("registered handler should be called for custom types");
            receivedMessage.Should().NotBeNull();
            receivedMessage!.TransactionId.Should().Be("txn-handler-test");
            _output.WriteLine("✓ FIXED: RegisterMessageHandler automatically registers custom types and works correctly");
        }

        [Fact]
        public async Task RegisterMessageHandler_MultipleCustomTypes_AllWork()
        {
            // Arrange
            _output.WriteLine("=== Test: Multiple custom message types work correctly ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var transactionHandlerCalled = false;
            var actionHandlerCalled = false;
            var tcs = new TaskCompletionSource<bool>();
            var handlersCompleted = 0;

            _fixture.Fixture.Client!.RegisterMessageHandler<CustomTransactionMessage>((message, senderId) =>
            {
                _output.WriteLine($"Transaction handler called: {message.TransactionId}");
                transactionHandlerCalled = true;
                Interlocked.Increment(ref handlersCompleted);
                if (handlersCompleted >= 2) tcs.TrySetResult(true);
            });

            _fixture.Fixture.Client!.RegisterMessageHandler<PlayerActionMessage>((message, senderId) =>
            {
                _output.WriteLine($"PlayerAction handler called: {message.ActionType}");
                actionHandlerCalled = true;
                Interlocked.Increment(ref handlersCompleted);
                if (handlersCompleted >= 2) tcs.TrySetResult(true);
            });

            // Send both types of messages
            await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                new CustomTransactionMessage { TransactionId = "txn-1", Amount = 50 }
            );

            await _fixture.Fixture.Client!.SendMessageToPlayerAsync(
                _fixture.Fixture.SteamId, 
                new PlayerActionMessage { ActionType = "ATTACK", ActionData = 100 }
            );

            await Task.WhenAny(tcs.Task, Task.Delay(2000));
            await Task.Delay(100);

            // Assert
            transactionHandlerCalled.Should().BeTrue("transaction handler should be called");
            actionHandlerCalled.Should().BeTrue("action handler should be called");
            _output.WriteLine("✓ Multiple custom message types all work correctly");
        }

        #endregion

        #region Message Serialization Integration Tests

        [Fact]
        public void MessageSerializer_SerializeAndDeserialize_TextMessage_RoundTrip()
        {
            // Arrange
            _output.WriteLine("=== Test: TextMessage serialization round-trip ===");
            
            var original = new TextMessage
            {
                SenderId = new CSteamID(76561197960265728),
                Content = "Round-trip test message"
            };

            // Act
            var serialized = MessageSerializer.SerializeMessage(original);
            var (messageType, messageData) = MessageSerializer.DeserializeMessage(serialized);
            var restored = MessageSerializer.CreateMessage<TextMessage>(serialized);

            // Assert
            messageType.Should().Be("TEXT");
            restored.Should().NotBeNull();
            restored!.Content.Should().Be("Round-trip test message");
            restored.SenderId.m_SteamID.Should().Be(76561197960265728);
            _output.WriteLine("✓ TextMessage round-trip serialization successful");
        }

        [Fact]
        public void MessageSerializer_SerializeAndDeserialize_CustomMessage_RoundTrip()
        {
            // Arrange
            _output.WriteLine("=== Test: Custom message serialization round-trip ===");
            
            var original = new CustomTransactionMessage
            {
                SenderId = new CSteamID(76561197960265728),
                TransactionId = "txn-roundtrip-123",
                FromPlayer = "Alice",
                ToPlayer = "Bob",
                Amount = 250.00m,
                Currency = "EUR",
                Description = "Payment for items"
            };

            // Act
            var serialized = MessageSerializer.SerializeMessage(original);
            var (messageType, messageData) = MessageSerializer.DeserializeMessage(serialized);
            var restored = MessageSerializer.CreateMessage<CustomTransactionMessage>(serialized);

            // Assert
            messageType.Should().Be("TRANSACTION");
            restored.Should().NotBeNull();
            restored!.TransactionId.Should().Be("txn-roundtrip-123");
            restored.FromPlayer.Should().Be("Alice");
            restored.ToPlayer.Should().Be("Bob");
            restored.Amount.Should().Be(250.00m);
            restored.Currency.Should().Be("EUR");
            restored.Description.Should().Be("Payment for items");
            _output.WriteLine("✓ Custom message round-trip serialization successful");
            _output.WriteLine("  Note: Serialization works fine, but P2P callbacks won't fire for custom types");
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _output.WriteLine("Cleaning up test...");
            _fixture.Fixture.LeaveLobby();
            _disposed = true;
        }

        /// <summary>
        /// Custom transaction message for testing custom message types.
        /// </summary>
        private class CustomTransactionMessage : P2PMessage
        {
            public override string MessageType => "TRANSACTION";

            public string TransactionId { get; set; } = string.Empty;
            public string FromPlayer { get; set; } = string.Empty;
            public string ToPlayer { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string Currency { get; set; } = "USD";
            public string Description { get; set; } = string.Empty;

            public override byte[] Serialize()
            {
                var json = System.Text.Json.JsonSerializer.Serialize(this);
                return System.Text.Encoding.UTF8.GetBytes(json);
            }

            public override void Deserialize(byte[] data)
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var deserialized = System.Text.Json.JsonSerializer.Deserialize<CustomTransactionMessage>(json);
                if (deserialized != null)
                {
                    TransactionId = deserialized.TransactionId;
                    FromPlayer = deserialized.FromPlayer;
                    ToPlayer = deserialized.ToPlayer;
                    Amount = deserialized.Amount;
                    Currency = deserialized.Currency;
                    Description = deserialized.Description;
                    SenderId = deserialized.SenderId;
                    Timestamp = deserialized.Timestamp;
                }
            }
        }

        /// <summary>
        /// Custom player action message for testing multiple custom message types.
        /// </summary>
        private class PlayerActionMessage : P2PMessage
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
    }
}
