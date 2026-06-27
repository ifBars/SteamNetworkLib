using System;
using System.Linq;
using FluentAssertions;
using SteamNetworkLib.Sync;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    public class NetworkSyncAttributeTests
    {
        [Fact]
        public void Discover_UsesMemberNameWhenNoExplicitKey()
        {
            var members = NetworkSyncBinder.Discover(typeof(ExampleState));

            members.Should().ContainSingle(member =>
                member.MemberName == nameof(ExampleState.RoundNumber) &&
                member.Key == nameof(ExampleState.RoundNumber) &&
                member.ValueType == typeof(int) &&
                member.Ownership == SyncedMemberOwnership.Host);
        }

        [Fact]
        public void Discover_UsesExplicitKeyWhenProvided()
        {
            var members = NetworkSyncBinder.Discover(typeof(ExampleState));

            members.Should().ContainSingle(member =>
                member.MemberName == nameof(ExampleState.Phase) &&
                member.Key == "event_phase" &&
                member.ValueType == typeof(string));
        }

        [Fact]
        public void Discover_IncludesPrivateFields()
        {
            var members = NetworkSyncBinder.Discover(typeof(ExampleState));

            members.Should().ContainSingle(member =>
                member.MemberName == "stockCount" &&
                member.Key == "stockCount" &&
                member.ValueType == typeof(int));
        }

        [Fact]
        public void DiscoverSynced_IncludesClientSyncedMembers()
        {
            var members = NetworkSyncBinder.DiscoverSynced(typeof(ExampleState));

            members.Should().ContainSingle(member =>
                member.MemberName == nameof(ExampleState.Ready) &&
                member.Key == "ready" &&
                member.ValueType == typeof(bool) &&
                member.Ownership == SyncedMemberOwnership.Client);
        }

        [Fact]
        public void Discover_DoesNotIncludeClientSyncedMembers()
        {
            var members = NetworkSyncBinder.Discover(typeof(ExampleState));

            members.Should().NotContain(member => member.MemberName == nameof(ExampleState.Ready));
        }

        [Fact]
        public void DiscoverSynced_RejectsMembersWithBothOwnershipAttributes()
        {
            Action act = () => NetworkSyncBinder.DiscoverSynced(typeof(ConflictingState));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*cannot be both host-synced and client-synced*");
        }

        [Fact]
        public void Discover_RejectsReadOnlyFields()
        {
            Action act = () => NetworkSyncBinder.Discover(typeof(ReadOnlyState));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*must be writable*");
        }

        [Fact]
        public void Discover_RejectsGetOnlyProperties()
        {
            Action act = () => NetworkSyncBinder.Discover(typeof(GetOnlyState));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*must have a setter*");
        }

        [Fact]
        public void DiscoverSynced_RejectsDuplicateKeys()
        {
            Action act = () => NetworkSyncBinder.DiscoverSynced(typeof(DuplicateKeyState));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Synced key 'shared_key' is used by more than one member*");
        }

        private sealed class ExampleState
        {
            [HostSynced]
            private int stockCount = 12;

            [HostSynced]
            public int RoundNumber { get; set; } = 1;

            [HostSynced("event_phase")]
            public string Phase { get; set; } = "setup";

            [ClientSynced("ready")]
            public bool Ready { get; set; }

            public int StockCountForCompiler => stockCount;
        }

        private sealed class ReadOnlyState
        {
            [HostSynced]
            private readonly int value = 1;

            public int ValueForCompiler => value;
        }

        private sealed class GetOnlyState
        {
            [HostSynced]
            public int Value => 1;
        }

        private sealed class ConflictingState
        {
            [HostSynced]
            [ClientSynced]
            public int Value { get; set; }
        }

        private sealed class DuplicateKeyState
        {
            [HostSynced("shared_key")]
            public int HostValue { get; set; }

            [ClientSynced("shared_key")]
            public int ClientValue { get; set; }
        }
    }
}
