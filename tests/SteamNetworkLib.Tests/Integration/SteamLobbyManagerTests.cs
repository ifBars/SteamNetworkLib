using System;
using System.Threading.Tasks;
using FluentAssertions;
using SteamNetworkLib.Tests.TestUtilities;
using Steamworks;
using Xunit;
using Xunit.Abstractions;

namespace SteamNetworkLib.Tests.Integration
{
    /// <summary>
    /// Integration tests for SteamLobbyManager functionality using Goldberg Steam Emulator.
    /// Tests lobby creation, leaving, and basic functionality with a single client.
    /// </summary>
    /// <remarks>
    /// Note: Multi-client tests (joining from another client) require multiple processes
    /// since Goldberg only supports one Steam instance per process.
    /// </remarks>
    [Collection("Steam Integration Tests")]
    public class SteamLobbyManagerTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly SteamCollectionFixture _fixture;
        private bool _disposed;

        public SteamLobbyManagerTests(SteamCollectionFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            
            // Leave any existing lobby before each test
            _fixture.Fixture.LeaveLobby();
        }

        [Fact]
        public async Task CreateLobbyAsync_CreatesLobbySuccessfully()
        {
            // Arrange
            _output.WriteLine("=== Test: Create lobby successfully ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            // Act
            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);

            // Assert
            lobbyId.Should().NotBe(CSteamID.Nil, "lobby should be created");
            _fixture.Fixture.Client!.IsInLobby.Should().BeTrue("client should be in lobby");
            _fixture.Fixture.Client!.IsHost.Should().BeTrue("creator should be host");

            _output.WriteLine($"✓ Created lobby: {lobbyId.m_SteamID}");
        }

        [Fact]
        public async Task CreateLobby_IsHost_ReturnsTrue()
        {
            // Arrange
            _output.WriteLine("=== Test: Creator is host ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            // Act
            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(100);

            // Assert
            _fixture.Fixture.Client!.IsHost.Should().BeTrue("creator should be host");

            _output.WriteLine("✓ IsHost correctly returns true for creator");
        }

        [Fact]
        public async Task LeaveLobby_WhenInLobby_LeavesSuccessfully()
        {
            // Arrange
            _output.WriteLine("=== Test: Leave lobby ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(100);
            _fixture.Fixture.Client!.IsInLobby.Should().BeTrue();

            // Act
            _fixture.Fixture.Client!.LeaveLobby();
            await Task.Delay(100);

            // Assert
            _fixture.Fixture.Client!.IsInLobby.Should().BeFalse("client should no longer be in lobby");

            _output.WriteLine("✓ Left lobby successfully");
        }

        [Fact]
        public async Task GetLobbyMembers_WhenAlone_ReturnsOneMember()
        {
            // Arrange
            _output.WriteLine("=== Test: Get lobby members when alone ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(500); // Give time for lobby to stabilize

            // Act
            var members = _fixture.Fixture.Client!.GetLobbyMembers();

            // Assert
            members.Should().HaveCount(1, "should have 1 member (just us)");
            
            _output.WriteLine($"✓ Found {members.Count} lobby member");
        }

        [Fact]
        public async Task IsInLobby_AfterCreating_ReturnsTrue()
        {
            // Arrange
            _output.WriteLine("=== Test: IsInLobby after creating ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            // Act
            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(100);

            // Assert
            _fixture.Fixture.Client!.IsInLobby.Should().BeTrue("should be in lobby after creating");

            _output.WriteLine("✓ IsInLobby correctly returns true after creating");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _output.WriteLine("Cleaning up test...");
            _fixture.Fixture.LeaveLobby();
            _disposed = true;
        }
    }
}
