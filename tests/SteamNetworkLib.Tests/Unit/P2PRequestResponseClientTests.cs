using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SteamNetworkLib.Core;
using SteamNetworkLib.Models;
using SteamNetworkLib.Utilities;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    public class P2PRequestResponseClientTests
    {
        private const ulong LocalSteamId = 76561198000000001UL;
        private const ulong ExpectedPeerSteamId = 76561198000000002UL;
        private const ulong OtherPeerSteamId = 76561198000000003UL;

        [Fact]
        public async Task SendRequestAsync_MatchingPeerResponse_CompletesRequest()
        {
            var (client, bridge) = CreateClient();
            using (client)
            using (var exchange = client.CreateRequestResponseClient<TestCheckoutRequestMessage, TestCheckoutResponseMessage>())
            {
                var request = CreateRequest();
                Task<TestCheckoutResponseMessage> pending = exchange.SendRequestAsync(
                    new CSteamID(ExpectedPeerSteamId),
                    request,
                    TimeSpan.FromSeconds(1));

                await bridge.WaitForSendCountAsync(1);
                bridge.InjectMessage(ExpectedPeerSteamId, CreateResponse(request.RequestId, "reservation-1"));

                TestCheckoutResponseMessage response = await pending;

                response.RequestId.Should().Be(request.RequestId);
                response.Body.ReservationId.Should().Be("reservation-1");
                exchange.PendingRequestCount.Should().Be(0);
            }
        }

        [Fact]
        public async Task SendRequestAsync_ResponseFromDifferentPeer_IsIgnored()
        {
            var (client, bridge) = CreateClient();
            using (client)
            using (var exchange = client.CreateRequestResponseClient<TestCheckoutRequestMessage, TestCheckoutResponseMessage>())
            {
                var request = CreateRequest();
                Task<TestCheckoutResponseMessage> pending = exchange.SendRequestAsync(
                    new CSteamID(ExpectedPeerSteamId),
                    request,
                    TimeSpan.FromSeconds(1));

                await bridge.WaitForSendCountAsync(1);
                bridge.InjectMessage(OtherPeerSteamId, CreateResponse(request.RequestId, "forged"));

                pending.IsCompleted.Should().BeFalse();
                exchange.PendingRequestCount.Should().Be(1);

                bridge.InjectMessage(ExpectedPeerSteamId, CreateResponse(request.RequestId, "expected"));
                TestCheckoutResponseMessage response = await pending;

                response.Body.ReservationId.Should().Be("expected");
            }
        }

        [Fact]
        public async Task SendRequestAsync_Timeout_RemovesPendingRequest()
        {
            var (client, bridge) = CreateClient();
            using (client)
            using (var exchange = client.CreateRequestResponseClient<TestCheckoutRequestMessage, TestCheckoutResponseMessage>())
            {
                Func<Task> send = async () => await exchange.SendRequestAsync(
                    new CSteamID(ExpectedPeerSteamId),
                    CreateRequest(),
                    TimeSpan.FromMilliseconds(20));

                await send.Should().ThrowAsync<TimeoutException>();
                bridge.Count("snl_dedicated_p2p_send").Should().Be(1);
                exchange.PendingRequestCount.Should().Be(0);
            }
        }

        [Fact]
        public async Task SendRequestAsync_SendFailure_RemovesPendingRequest()
        {
            var (client, bridge) = CreateClient();
            bridge.SendSucceeds = false;

            using (client)
            using (var exchange = client.CreateRequestResponseClient<TestCheckoutRequestMessage, TestCheckoutResponseMessage>())
            {
                Func<Task> send = async () => await exchange.SendRequestAsync(
                    new CSteamID(ExpectedPeerSteamId),
                    CreateRequest(),
                    TimeSpan.FromSeconds(1));

                await send.Should().ThrowAsync<InvalidOperationException>();
                exchange.PendingRequestCount.Should().Be(0);
            }
        }

        [Fact]
        public async Task Dispose_PendingRequest_FailsWithObjectDisposedException()
        {
            var (client, bridge) = CreateClient();
            using (client)
            {
                var exchange = client.CreateRequestResponseClient<TestCheckoutRequestMessage, TestCheckoutResponseMessage>();
                Task<TestCheckoutResponseMessage> pending = exchange.SendRequestAsync(
                    new CSteamID(ExpectedPeerSteamId),
                    CreateRequest(),
                    TimeSpan.FromSeconds(1));

                await bridge.WaitForSendCountAsync(1);
                exchange.Dispose();

                Func<Task> observePending = async () => await pending;
                await observePending.Should().ThrowAsync<ObjectDisposedException>();
                exchange.PendingRequestCount.Should().Be(0);
            }
        }

        [Fact]
        public async Task RegisterResponder_RequestArrives_SendsCorrelatedResponse()
        {
            var (client, bridge) = CreateClient();
            using (client)
            using (var exchange = client.CreateRequestResponseClient<TestCheckoutRequestMessage, TestCheckoutResponseMessage>())
            {
                exchange.RegisterResponder((request, senderId) =>
                    new TestCheckoutResponseMessage(new TestCheckoutResponsePayload
                    {
                        ReservationId = $"reservation-{senderId.m_SteamID}",
                        ApprovedQuantity = request.Body.Quantity
                    })
                    {
                        Success = true
                    });

                bridge.InjectMessage(ExpectedPeerSteamId, CreateRequest("request-1"));
                await bridge.WaitForSendCountAsync(1);

                TestCheckoutResponseMessage response = bridge.ReadLastSentMessage<TestCheckoutResponseMessage>();
                response.RequestId.Should().Be("request-1");
                response.Success.Should().BeTrue();
                response.Body.ApprovedQuantity.Should().Be(12);
                response.Body.ReservationId.Should().Be($"reservation-{ExpectedPeerSteamId}");
            }
        }

        [Fact]
        public async Task RegisterResponder_ResponderThrows_SendsFailureResponse()
        {
            var (client, bridge) = CreateClient();
            using (client)
            using (var exchange = client.CreateRequestResponseClient<TestCheckoutRequestMessage, TestCheckoutResponseMessage>())
            {
                Func<TestCheckoutRequestMessage, CSteamID, TestCheckoutResponseMessage> responder =
                    (_, _) => throw new InvalidOperationException("reservation failed");
                exchange.RegisterResponder(responder);

                bridge.InjectMessage(ExpectedPeerSteamId, CreateRequest("request-2"));
                await bridge.WaitForSendCountAsync(1);

                TestCheckoutResponseMessage response = bridge.ReadLastSentMessage<TestCheckoutResponseMessage>();
                response.RequestId.Should().Be("request-2");
                response.Success.Should().BeFalse();
                response.Error.Should().Be("Request responder failed.");
            }
        }

        [Fact]
        public async Task Dispose_RegisteredResponder_StopsHandlingRequests()
        {
            var (client, bridge) = CreateClient();
            using (client)
            {
                var exchange = client.CreateRequestResponseClient<TestCheckoutRequestMessage, TestCheckoutResponseMessage>();
                exchange.RegisterResponder((request, senderId) => CreateResponse(request.RequestId, "unused"));
                exchange.Dispose();

                bridge.InjectMessage(ExpectedPeerSteamId, CreateRequest("after-dispose"));
                await Task.Delay(20);

                bridge.Count("snl_dedicated_p2p_send").Should().Be(0);
            }
        }

        [Fact]
        public async Task SendRequestAsync_DisposeWinsBeforeRegistration_DoesNotSend()
        {
            var (client, bridge) = CreateClient();
            using (client)
            {
                var exchange = client.CreateRequestResponseClient<BlockingRequestMessage, TestCheckoutResponseMessage>();
                var request = new BlockingRequestMessage { BlockRequestIdRead = true };

                Task<TestCheckoutResponseMessage> send = Task.Run(async () => await exchange.SendRequestAsync(
                    new CSteamID(ExpectedPeerSteamId),
                    request,
                    TimeSpan.FromSeconds(1)));

                request.RequestIdReadStarted.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
                exchange.Dispose();
                request.ContinueRequestIdRead.Set();

                Func<Task> observeSend = async () => await send;
                await observeSend.Should().ThrowAsync<ObjectDisposedException>();
                bridge.Count("snl_dedicated_p2p_send").Should().Be(0);
            }
        }

        [Fact]
        public async Task RegisterResponder_DisposeWinsDuringSubscription_UnregistersHandler()
        {
            var (client, bridge) = CreateClient();
            using (client)
            {
                var exchange = client.CreateRequestResponseClient<BlockingResponderRequestMessage, TestCheckoutResponseMessage>();
                BlockingResponderRequestMessage.ResetGate();
                BlockingResponderRequestMessage.BlockMessageTypeRead = true;

                try
                {
                    Task<IDisposable> register = Task.Run(() => exchange.RegisterResponder(
                        (BlockingResponderRequestMessage _, CSteamID _) => CreateResponse("unused", "unused")));

                    BlockingResponderRequestMessage.MessageTypeReadStarted.Wait(TimeSpan.FromSeconds(1)).Should().BeTrue();
                    exchange.Dispose();
                    BlockingResponderRequestMessage.ContinueMessageTypeRead.Set();

                    Func<Task> observeRegistration = async () => await register;
                    await observeRegistration.Should().ThrowAsync<ObjectDisposedException>();

                    bridge.InjectMessage(ExpectedPeerSteamId, new BlockingResponderRequestMessage
                    {
                        RequestId = "after-dispose"
                    });
                    await Task.Delay(20);

                    bridge.Count("snl_dedicated_p2p_send").Should().Be(0);
                }
                finally
                {
                    BlockingResponderRequestMessage.BlockMessageTypeRead = false;
                    BlockingResponderRequestMessage.ContinueMessageTypeRead.Set();
                }
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_InvalidDefaultTimeout_Throws(double milliseconds)
        {
            var (client, _) = CreateClient();
            using (client)
            {
                Action create = () => client.CreateRequestResponseClient<TestCheckoutRequestMessage, TestCheckoutResponseMessage>(
                    TimeSpan.FromMilliseconds(milliseconds));

                create.Should().Throw<ArgumentOutOfRangeException>();
            }
        }

        [Fact]
        public async Task SendRequestAsync_TimeoutLongerThanTaskDelaySupports_DoesNotSend()
        {
            var (client, bridge) = CreateClient();
            using (client)
            using (var exchange = client.CreateRequestResponseClient<TestCheckoutRequestMessage, TestCheckoutResponseMessage>())
            {
                Func<Task> send = async () => await exchange.SendRequestAsync(
                    new CSteamID(ExpectedPeerSteamId),
                    CreateRequest(),
                    TimeSpan.FromMilliseconds((double)int.MaxValue + 1));

                await send.Should().ThrowAsync<ArgumentOutOfRangeException>();
                bridge.Count("snl_dedicated_p2p_send").Should().Be(0);
            }
        }

        private static (SteamNetworkClient Client, FakeDedicatedBridge Bridge) CreateClient()
        {
            var bridge = new FakeDedicatedBridge();
            var client = new SteamNetworkClient(new NetworkRules(), () => false, () => bridge);
            client.Initialize();
            bridge.Emit("snl_dedicated_snapshot", CreateSnapshot());
            return (client, bridge);
        }

        private static TestCheckoutRequestMessage CreateRequest(string requestId = "")
        {
            return new TestCheckoutRequestMessage(new TestCheckoutRequestPayload
            {
                ItemId = "pseudo",
                Quantity = 12
            })
            {
                RequestId = requestId
            };
        }

        private static TestCheckoutResponseMessage CreateResponse(string requestId, string reservationId)
        {
            return new TestCheckoutResponseMessage(new TestCheckoutResponsePayload
            {
                ReservationId = reservationId,
                ApprovedQuantity = 12
            })
            {
                RequestId = requestId,
                Success = true
            };
        }

        private static string CreateSnapshot()
        {
            return "{" +
                   "\"SessionId\":\"request-response-tests\"," +
                   $"\"LocalSteamId\":\"{LocalSteamId}\"," +
                   $"\"OwnerSteamId\":\"{ExpectedPeerSteamId}\"," +
                   $"\"ServerSteamId\":\"{ExpectedPeerSteamId}\"," +
                   "\"Members\":[" +
                   $"{{\"SteamId\":\"{LocalSteamId}\",\"DisplayName\":\"Client\",\"IsOwner\":false,\"IsLocalPlayer\":true,\"JoinedAtUnixMs\":1}}," +
                   $"{{\"SteamId\":\"{ExpectedPeerSteamId}\",\"DisplayName\":\"Host\",\"IsOwner\":true,\"IsLocalPlayer\":false,\"JoinedAtUnixMs\":2}}," +
                   $"{{\"SteamId\":\"{OtherPeerSteamId}\",\"DisplayName\":\"Other\",\"IsOwner\":false,\"IsLocalPlayer\":false,\"JoinedAtUnixMs\":3}}" +
                   "],\"LobbyData\":{},\"MemberData\":{}}";
        }

        private sealed class FakeDedicatedBridge : IDedicatedServerMessagingBridge
        {
            private readonly object _syncRoot = new object();
            private readonly List<(string Command, string Payload)> _commands =
                new List<(string Command, string Payload)>();

            public event Action<string, string>? MessageReceived;

            public event Action? EndpointReady
            {
                add { }
                remove { }
            }

            public bool IsDedicatedContextLikely { get; private set; }

            public bool SendSucceeds { get; set; } = true;

            public bool TrySendToServer(string command, string payload)
            {
                lock (_syncRoot)
                {
                    _commands.Add((command, payload));
                }

                return SendSucceeds;
            }

            public int Count(string command)
            {
                lock (_syncRoot)
                {
                    return _commands.Count(x => x.Command == command);
                }
            }

            public void Emit(string command, string payload)
            {
                IsDedicatedContextLikely = true;
                MessageReceived?.Invoke(command, payload);
            }

            public void InjectMessage(ulong senderSteamId, P2PMessage message)
            {
                byte[] packet = MessageSerializer.SerializeMessage(message);
                Emit(
                    "snl_dedicated_p2p_message",
                    "{" +
                    $"\"SenderSteamId\":\"{senderSteamId}\"," +
                    $"\"DataBase64\":\"{Convert.ToBase64String(packet)}\"," +
                    "\"Channel\":0}");
            }

            public T ReadLastSentMessage<T>() where T : P2PMessage, new()
            {
                string payload;
                lock (_syncRoot)
                {
                    payload = _commands.Last(x => x.Command == "snl_dedicated_p2p_send").Payload;
                }

                using JsonDocument document = JsonDocument.Parse(payload);
                string dataBase64 = document.RootElement.GetProperty("DataBase64").GetString()!;
                return MessageSerializer.CreateMessage<T>(Convert.FromBase64String(dataBase64));
            }

            public async Task WaitForSendCountAsync(int expectedCount)
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(1);
                while (Count("snl_dedicated_p2p_send") < expectedCount && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(10);
                }

                Count("snl_dedicated_p2p_send").Should().BeGreaterOrEqualTo(expectedCount);
            }

            public void Dispose()
            {
            }
        }

        private sealed class BlockingRequestMessage : P2PMessage, IP2PCorrelatedMessage
        {
            private string _requestId = string.Empty;

            public BlockingRequestMessage()
            {
            }

            public override string MessageType => "BLOCKING_REQUEST";

            public bool BlockRequestIdRead { get; set; }

            public ManualResetEventSlim RequestIdReadStarted { get; } = new ManualResetEventSlim(false);

            public ManualResetEventSlim ContinueRequestIdRead { get; } = new ManualResetEventSlim(false);

            public string RequestId
            {
                get
                {
                    if (BlockRequestIdRead)
                    {
                        RequestIdReadStarted.Set();
                        ContinueRequestIdRead.Wait(TimeSpan.FromSeconds(2));
                    }

                    return _requestId;
                }
                set => _requestId = value ?? string.Empty;
            }

            public override byte[] Serialize()
            {
                return Encoding.UTF8.GetBytes(RequestId);
            }

            public override void Deserialize(byte[] data)
            {
                RequestId = Encoding.UTF8.GetString(data);
            }
        }

        private sealed class BlockingResponderRequestMessage : P2PMessage, IP2PCorrelatedMessage
        {
            public BlockingResponderRequestMessage()
            {
            }

            public static bool BlockMessageTypeRead { get; set; }

            public static ManualResetEventSlim MessageTypeReadStarted { get; private set; } =
                new ManualResetEventSlim(false);

            public static ManualResetEventSlim ContinueMessageTypeRead { get; private set; } =
                new ManualResetEventSlim(false);

            public override string MessageType
            {
                get
                {
                    if (BlockMessageTypeRead)
                    {
                        MessageTypeReadStarted.Set();
                        ContinueMessageTypeRead.Wait(TimeSpan.FromSeconds(2));
                    }

                    return "BLOCKING_RESPONDER_REQUEST";
                }
            }

            public string RequestId { get; set; } = string.Empty;

            public static void ResetGate()
            {
                MessageTypeReadStarted.Dispose();
                ContinueMessageTypeRead.Dispose();
                MessageTypeReadStarted = new ManualResetEventSlim(false);
                ContinueMessageTypeRead = new ManualResetEventSlim(false);
            }

            public override byte[] Serialize()
            {
                return Encoding.UTF8.GetBytes(RequestId);
            }

            public override void Deserialize(byte[] data)
            {
                RequestId = Encoding.UTF8.GetString(data);
            }
        }
    }
}
