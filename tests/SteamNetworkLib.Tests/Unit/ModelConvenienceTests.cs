using FluentAssertions;
using SteamNetworkLib.Models;
using Steamworks;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    public class ModelConvenienceTests
    {
        [Fact]
        public void MemberInfo_SteamId64_SetsAndGetsNativeSteamId()
        {
            var member = new MemberInfo
            {
                SteamId64 = 76561197960265728
            };

            member.SteamId.m_SteamID.Should().Be(76561197960265728);
            member.SteamId64.Should().Be(76561197960265728);
            member.SteamIdString.Should().Be("76561197960265728");
        }

        [Fact]
        public void LobbyInfo_UlongIds_SetAndGetNativeSteamIds()
        {
            var lobby = new LobbyInfo
            {
                LobbyId64 = 109775242466683779,
                OwnerId64 = 76561197960265728
            };

            lobby.LobbyId.m_SteamID.Should().Be(109775242466683779);
            lobby.OwnerId.m_SteamID.Should().Be(76561197960265728);
            lobby.LobbyId64.Should().Be(109775242466683779);
            lobby.OwnerId64.Should().Be(76561197960265728);
        }

        [Fact]
        public void MemberInfo_SteamId64_ReflectsNativeSteamIdAssignment()
        {
            var member = new MemberInfo
            {
                SteamId = new CSteamID(123456789)
            };

            member.SteamId64.Should().Be(123456789);
            member.SteamIdString.Should().Be("123456789");
        }
    }
}
