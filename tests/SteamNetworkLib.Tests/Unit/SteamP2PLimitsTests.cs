using FluentAssertions;
using SteamNetworkLib.Core;
using Steamworks;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    public class SteamP2PLimitsTests
    {
        [Theory]
        [InlineData(EP2PSend.k_EP2PSendUnreliable)]
        [InlineData(EP2PSend.k_EP2PSendUnreliableNoDelay)]
        public void GetMaxPacketSize_UnreliableSendTypes_ReturnsSteamMtuLimit(EP2PSend sendType)
        {
            SteamP2PLimits.GetMaxPacketSize(sendType).Should().Be(1200);
        }

        [Theory]
        [InlineData(EP2PSend.k_EP2PSendReliable)]
        [InlineData(EP2PSend.k_EP2PSendReliableWithBuffering)]
        public void GetMaxPacketSize_ReliableSendTypes_ReturnsSteamReliableLimit(EP2PSend sendType)
        {
            SteamP2PLimits.GetMaxPacketSize(sendType).Should().Be(1024 * 1024);
        }

        [Theory]
        [InlineData(EP2PSend.k_EP2PSendUnreliable, false)]
        [InlineData(EP2PSend.k_EP2PSendUnreliableNoDelay, false)]
        [InlineData(EP2PSend.k_EP2PSendReliable, true)]
        [InlineData(EP2PSend.k_EP2PSendReliableWithBuffering, true)]
        public void IsReliable_ReturnsExpectedValue(EP2PSend sendType, bool expected)
        {
            SteamP2PLimits.IsReliable(sendType).Should().Be(expected);
        }
    }
}
