using System;
using System.Linq;
using FluentAssertions;
using SteamNetworkLib.Sync;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    public class HostSyncedAttributeTests
    {
        [Fact]
        public void Discover_UsesMemberNameWhenNoExplicitKey()
        {
            var members = HostSyncedBinder.Discover(typeof(ExampleState));

            members.Should().ContainSingle(member =>
                member.MemberName == nameof(ExampleState.RoundNumber) &&
                member.Key == nameof(ExampleState.RoundNumber) &&
                member.ValueType == typeof(int));
        }

        [Fact]
        public void Discover_UsesExplicitKeyWhenProvided()
        {
            var members = HostSyncedBinder.Discover(typeof(ExampleState));

            members.Should().ContainSingle(member =>
                member.MemberName == nameof(ExampleState.Phase) &&
                member.Key == "event_phase" &&
                member.ValueType == typeof(string));
        }

        [Fact]
        public void Discover_IncludesPrivateFields()
        {
            var members = HostSyncedBinder.Discover(typeof(ExampleState));

            members.Should().ContainSingle(member =>
                member.MemberName == "stockCount" &&
                member.Key == "stockCount" &&
                member.ValueType == typeof(int));
        }

        [Fact]
        public void Discover_RejectsReadOnlyFields()
        {
            Action act = () => HostSyncedBinder.Discover(typeof(ReadOnlyState));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*must be writable*");
        }

        [Fact]
        public void Discover_RejectsGetOnlyProperties()
        {
            Action act = () => HostSyncedBinder.Discover(typeof(GetOnlyState));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*must have a setter*");
        }

        private sealed class ExampleState
        {
            [HostSynced]
            private int stockCount = 12;

            [HostSynced]
            public int RoundNumber { get; set; } = 1;

            [HostSynced("event_phase")]
            public string Phase { get; set; } = "setup";

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
    }
}
