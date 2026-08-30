using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using SteamNetworkLib.Core;
using SteamNetworkLib.Models;
using SteamNetworkLib.Utilities;
using Steamworks;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    public class DedicatedRelayInitializationTests
    {
        private const ulong RemoteSteamId = 76561198000000002UL;
        private const ulong ServerSteamId = 90190190190190190UL;

        [Fact]
        public async Task Initialize_WithoutClientSteam_UsesDedicatedRelayBackend()
        {
            var bridge = new FakeDedicatedBridge();
            using var client = new SteamNetworkClient(new NetworkRules(), () => false, () => bridge);

            client.Initialize().Should().BeTrue();
            client.IsInitialized.Should().BeTrue();
            client.LobbyManager.Should().BeNull();
            client.LobbyData.Should().BeNull();
            client.MemberData.Should().BeNull();
            client.P2PManager.Should().NotBeNull();
            client.P2PManager!.IsActive.Should().BeTrue();
            bridge.Commands.Should().ContainSingle(x => x.Command == "snl_dedicated_register");

            bridge.Emit("snl_dedicated_snapshot", CreateSnapshot());

            client.SessionMode.Should().Be(NetworkSessionMode.DedicatedRelay);
            client.IsInLobby.Should().BeTrue();
            client.IsHost.Should().BeTrue();
            client.LocalPlayerId64.Should().Be(ServerSteamId);
            client.HostPlayerId64.Should().Be(ServerSteamId);
            client.GetLobbyMembers().Should().HaveCount(2);
            client.GetPlayerData(new CSteamID(ServerSteamId), "role").Should().Be("server");

            int registerCountAfterSnapshot = bridge.Count("snl_dedicated_register");
            bridge.EmitEndpointReady();
            bridge.Count("snl_dedicated_register").Should().Be(registerCountAfterSnapshot);

            client.SetLobbyData("__tns_current_time", "1400");
            bridge.Commands.Should().Contain(x => x.Command == "snl_dedicated_set_lobby_data");

            bool sent = await client.SendMessageToPlayerAsync(
                new CSteamID(RemoteSteamId),
                new TextMessage { Content = "relay" });

            sent.Should().BeTrue();
            bridge.Commands.Should().Contain(x => x.Command == "snl_dedicated_p2p_send");

            TextMessage? receivedMessage = null;
            ulong receivedSender = 0;
            int receivedChannel = -1;
            client.OnP2PMessageReceived += (_, args) =>
            {
                receivedMessage = args.Message as TextMessage;
                receivedSender = args.SenderId.m_SteamID;
                receivedChannel = args.Channel;
            };

            byte[] incomingPacket = MessageSerializer.SerializeMessage(new TextMessage { Content = "from relay" });
            bridge.Emit(
                "snl_dedicated_p2p_message",
                "{" +
                $"\"SenderSteamId\":\"{RemoteSteamId}\"," +
                $"\"DataBase64\":\"{Convert.ToBase64String(incomingPacket)}\"," +
                "\"Channel\":3}");

            receivedMessage.Should().NotBeNull();
            receivedMessage!.Content.Should().Be("from relay");
            receivedSender.Should().Be(RemoteSteamId);
            receivedChannel.Should().Be(3);
            Action process = client.ProcessIncomingMessages;
            process.Should().NotThrow();
        }

        [Fact]
        public void Initialize_WithoutAnyBackend_ThrowsRetryableSteamUnavailableError()
        {
            using var client = new SteamNetworkClient(new NetworkRules(), () => false, () => null);

            Action initialize = () => client.Initialize();

            initialize.Should().Throw<SteamNetworkLib.Exceptions.SteamNetworkException>()
                .Where(error => error.IsRetryable)
                .Where(error => error.ErrorKind == SteamNetworkLib.Exceptions.SteamNetworkErrorKind.SteamUnavailable);
        }

        [Fact]
        public void EndpointReady_RetriesDedicatedRegistration()
        {
            var bridge = new FakeDedicatedBridge { SendSucceeds = false };
            using var client = new SteamNetworkClient(new NetworkRules(), () => false, () => bridge);

            client.Initialize();
            int initialRegisterCount = bridge.Count("snl_dedicated_register");

            bridge.SendSucceeds = true;
            bridge.EmitEndpointReady();

            bridge.Count("snl_dedicated_register").Should().Be(initialRegisterCount + 1);
        }

        private static string CreateSnapshot()
        {
            return "{" +
                   "\"SessionId\":\"test-session\"," +
                   "\"LocalSteamId\":\"0\"," +
                   "\"OwnerSteamId\":\"0\"," +
                   $"\"ServerSteamId\":\"{ServerSteamId}\"," +
                   "\"Members\":[" +
                   "{\"SteamId\":\"0\",\"DisplayName\":\"Host\",\"IsOwner\":true,\"IsLocalPlayer\":true,\"JoinedAtUnixMs\":1}," +
                   $"{{\"SteamId\":\"{RemoteSteamId}\",\"DisplayName\":\"Client\",\"IsOwner\":false,\"IsLocalPlayer\":false,\"JoinedAtUnixMs\":2}}" +
                   "],\"LobbyData\":{},\"MemberData\":{\"0\":{\"role\":\"server\"}}}";
        }

        private sealed class FakeDedicatedBridge : IDedicatedServerMessagingBridge
        {
            public event Action<string, string>? MessageReceived;

            public event Action? EndpointReady;

            public bool IsDedicatedContextLikely { get; private set; }

            public bool SendSucceeds { get; set; } = true;

            public List<(string Command, string Payload)> Commands { get; } =
                new List<(string Command, string Payload)>();

            public bool TrySendToServer(string command, string payload)
            {
                Commands.Add((command, payload));
                return SendSucceeds;
            }

            public int Count(string command)
            {
                return Commands.FindAll(x => x.Command == command).Count;
            }

            public void Emit(string command, string payload)
            {
                IsDedicatedContextLikely = true;
                MessageReceived?.Invoke(command, payload);
            }

            public void EmitEndpointReady()
            {
                IsDedicatedContextLikely = true;
                EndpointReady?.Invoke();
            }

            public void Dispose()
            {
            }
        }
    }
}
