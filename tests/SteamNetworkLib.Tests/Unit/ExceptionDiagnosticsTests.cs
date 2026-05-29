using FluentAssertions;
using SteamNetworkLib.Exceptions;
using SteamNetworkLib.Sync;
using Steamworks;
using System;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    public class ExceptionDiagnosticsTests
    {
        [Fact]
        public void SteamNetworkException_WithDiagnostics_ExposesRetryAndOperation()
        {
            var exception = new SteamNetworkException(
                "Steam unavailable",
                SteamNetworkErrorKind.SteamUnavailable,
                operation: "Initialize",
                isRetryable: true);

            exception.ErrorKind.Should().Be(SteamNetworkErrorKind.SteamUnavailable);
            exception.Operation.Should().Be("Initialize");
            exception.IsRetryable.Should().BeTrue();
        }

        [Fact]
        public void LobbyException_WithContext_ExposesRuntimeNeutralIdsAndDataKey()
        {
            var lobbyId = new CSteamID(76561197960265728UL);
            var memberId = new CSteamID(76561197960265729UL);

            var exception = new LobbyException(
                "Failed to set lobby data",
                SteamNetworkErrorKind.LobbyDataFailed,
                operation: "SetData",
                lobbyId: lobbyId,
                memberId: memberId,
                dataKey: "AutoRestock_Config",
                requiresHost: true);

            exception.ErrorKind.Should().Be(SteamNetworkErrorKind.LobbyDataFailed);
            exception.Operation.Should().Be("SetData");
            exception.LobbyId64.Should().Be(76561197960265728UL);
            exception.MemberId64.Should().Be(76561197960265729UL);
            exception.DataKey.Should().Be("AutoRestock_Config");
            exception.RequiresHost.Should().BeTrue();
        }

        [Fact]
        public void P2PException_WithContext_ExposesPacketAndMessageDetails()
        {
            var targetId = new CSteamID(76561197960265730UL);

            var exception = new P2PException(
                "Packet too large",
                SteamNetworkErrorKind.PacketTooLarge,
                operation: "SendPacketAsync",
                targetId: targetId,
                channel: 2,
                messageType: "AUTORESTOCK_TRANSACTION",
                packetSize: 2048,
                maxPacketSize: 1200);

            exception.ErrorKind.Should().Be(SteamNetworkErrorKind.PacketTooLarge);
            exception.Operation.Should().Be("SendPacketAsync");
            exception.TargetId64.Should().Be(76561197960265730UL);
            exception.HasChannel.Should().BeTrue();
            exception.Channel.Should().Be(2);
            exception.MessageType.Should().Be("AUTORESTOCK_TRANSACTION");
            exception.PacketSize.Should().Be(2048);
            exception.MaxPacketSize.Should().Be(1200);
        }

        [Fact]
        public void SyncException_InExceptionsNamespace_IsSteamNetworkException()
        {
            var inner = new InvalidOperationException("inner");
            var exception = new SteamNetworkLib.Exceptions.SyncException(
                "Sync failed",
                "AutoRestock_State",
                inner,
                SteamNetworkErrorKind.MemberDataFailed,
                operation: "FlushPending",
                isRetryable: true);

            exception.Should().BeAssignableTo<SteamNetworkException>();
            exception.SyncKey.Should().Be("AutoRestock_State");
            exception.InnerException.Should().BeSameAs(inner);
            exception.ErrorKind.Should().Be(SteamNetworkErrorKind.MemberDataFailed);
            exception.Operation.Should().Be("FlushPending");
            exception.IsRetryable.Should().BeTrue();
        }

        [Fact]
        public void SpecializedSyncExceptions_AreSteamNetworkExceptions()
        {
            var serializationException = new SyncSerializationException(
                "Serialization failed",
                typeof(string),
                "AutoRestock_State");
            var validationException = new SyncValidationException(
                "Validation failed",
                "AutoRestock_State",
                invalidValue: null);

            serializationException.Should().BeAssignableTo<SteamNetworkException>();
            serializationException.ErrorKind.Should().Be(SteamNetworkErrorKind.SerializationFailed);
            serializationException.SyncKey.Should().Be("AutoRestock_State");
            serializationException.TargetType.Should().Be(typeof(string));

            validationException.Should().BeAssignableTo<SteamNetworkException>();
            validationException.ErrorKind.Should().Be(SteamNetworkErrorKind.ValidationFailed);
            validationException.SyncKey.Should().Be("AutoRestock_State");
        }
    }
}
