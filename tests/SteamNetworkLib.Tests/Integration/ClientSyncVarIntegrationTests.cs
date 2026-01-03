using System;
using System.Threading.Tasks;
using FluentAssertions;
using SteamNetworkLib.Sync;
using SteamNetworkLib.Tests.TestUtilities;
using Steamworks;
using Xunit;
using Xunit.Abstractions;

namespace SteamNetworkLib.Tests.Integration
{
    /// <summary>
    /// Integration tests for ClientSyncVar using Goldberg Steam Emulator.
    /// Tests client-owned sync vars with a single client (self-sync scenarios).
    /// </summary>
    /// <remarks>
    /// Note: Multi-client tests require multiple processes since Goldberg
    /// only supports one Steam instance per process.
    /// </remarks>
    [Collection("Steam Integration Tests")]
    public class ClientSyncVarIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly SteamCollectionFixture _fixture;
        private bool _disposed;

        public ClientSyncVarIntegrationTests(SteamCollectionFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            
            // Leave any existing lobby before each test
            _fixture.Fixture.LeaveLobby();
        }

        [Fact]
        public async Task ClientSyncVar_SetValue_ValueUpdates()
        {
            // Arrange
            _output.WriteLine("=== Test: Client sets value, value updates ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            lobbyId.Should().NotBe(CSteamID.Nil, "lobby should be created");
            await Task.Delay(200);

            // Act - Create sync var and set value
            var syncVar = _fixture.Fixture.Client!.CreateClientSyncVar("PlayerReady", false);
            syncVar.Value = true;

            // Assert
            syncVar.Value.Should().BeTrue("value should be set correctly");

            _output.WriteLine("✓ Test passed: Client sync var value updated correctly");
        }

        [Fact]
        public async Task ClientSyncVar_OnValueChanged_FiresOnChange()
        {
            // Arrange
            _output.WriteLine("=== Test: OnValueChanged event fires ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var syncVar = _fixture.Fixture.Client!.CreateClientSyncVar("PlayerScore", 0);
            
            CSteamID? changedPlayerId = null;
            int? oldValue = null;
            int? newValue = null;
            syncVar.OnValueChanged += (playerId, old, @new) =>
            {
                changedPlayerId = playerId;
                oldValue = old;
                newValue = @new;
                _output.WriteLine($"Player {playerId.m_SteamID} value changed: {old} -> {@new}");
            };

            // Act
            syncVar.Value = 500;

            // Assert
            changedPlayerId.Should().NotBeNull("should receive player ID");
            changedPlayerId.Value.m_SteamID.Should().Be(_fixture.Fixture.SteamId.m_SteamID, "should be our own ID");
            oldValue.Should().Be(0, "old value should be initial value");
            newValue.Should().Be(500, "new value should be set value");

            _output.WriteLine("✓ Test passed: OnValueChanged fired correctly");
        }

        [Fact]
        public async Task ClientSyncVar_GetValue_ReturnsOwnValue()
        {
            // Arrange
            _output.WriteLine("=== Test: GetValue returns own value ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var syncVar = _fixture.Fixture.Client!.CreateClientSyncVar("Loadout", "None");
            syncVar.Value = "Rifle";

            // Act
            var value = syncVar.GetValue(_fixture.Fixture.SteamId);

            // Assert
            value.Should().Be("Rifle", "should return our own value");

            _output.WriteLine("✓ Test passed: GetValue returns correct value");
        }

        [Fact]
        public async Task ClientSyncVar_GetAllValues_ReturnsOwnValue()
        {
            // Arrange
            _output.WriteLine("=== Test: GetAllValues returns own value ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var syncVar = _fixture.Fixture.Client!.CreateClientSyncVar("Health", 100);
            syncVar.Value = 75;
            await Task.Delay(100);

            // Act
            var allValues = syncVar.GetAllValues();

            // Assert
            allValues.Should().HaveCountGreaterOrEqualTo(1, "should have at least our own value");
            allValues.Should().ContainKey(_fixture.Fixture.SteamId, "should contain our Steam ID");
            allValues[_fixture.Fixture.SteamId].Should().Be(75, "our value should be 75");

            _output.WriteLine($"✓ Test passed: GetAllValues returned {allValues.Count} player value(s)");
        }

        [Fact]
        public async Task ClientSyncVar_ComplexObject_SynchronizesCorrectly()
        {
            // Arrange
            _output.WriteLine("=== Test: Complex object synchronization ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            var defaultLoadout = new PlayerLoadout { Weapon = "None", Armor = "None", Level = 0 };
            var syncVar = _fixture.Fixture.Client!.CreateClientSyncVar("Loadout", defaultLoadout);

            // Act
            var newLoadout = new PlayerLoadout { Weapon = "Rifle", Armor = "Heavy", Level = 10 };
            syncVar.Value = newLoadout;

            // Assert
            syncVar.Value.Should().NotBeNull();
            syncVar.Value.Weapon.Should().Be("Rifle");
            syncVar.Value.Armor.Should().Be("Heavy");
            syncVar.Value.Level.Should().Be(10);

            _output.WriteLine("✓ Test passed: Complex object synchronized correctly");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _output.WriteLine("Cleaning up test...");
            _fixture.Fixture.LeaveLobby();
            _disposed = true;
        }
    }

    /// <summary>
    /// Test class for player loadout synchronization.
    /// </summary>
    public class PlayerLoadout
    {
        public string Weapon { get; set; } = string.Empty;
        public string Armor { get; set; } = string.Empty;
        public int Level { get; set; }
    }
}
