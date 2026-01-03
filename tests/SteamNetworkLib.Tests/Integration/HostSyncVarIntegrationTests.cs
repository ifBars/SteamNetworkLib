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
    /// Integration tests for HostSyncVar using Goldberg Steam Emulator.
    /// Tests host-authoritative sync vars with a single client (self-sync scenarios).
    /// </summary>
    /// <remarks>
    /// Note: Multi-client tests require multiple processes since Goldberg
    /// only supports one Steam instance per process.
    /// </remarks>
    [Collection("Steam Integration Tests")]
    public class HostSyncVarIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly SteamCollectionFixture _fixture;
        private bool _disposed;

        public HostSyncVarIntegrationTests(SteamCollectionFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            
            // Leave any existing lobby before each test
            _fixture.Fixture.LeaveLobby();
        }

        [Fact]
        public async Task HostSyncVar_SetValue_ValueUpdates()
        {
            // Arrange
            _output.WriteLine("=== Test: Host sets value, value updates ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            lobbyId.Should().NotBe(CSteamID.Nil, "lobby should be created");
            await Task.Delay(200);

            // Act - Create sync var and set value
            var syncVar = _fixture.Fixture.Client!.CreateHostSyncVar("TestValue", 0);
            syncVar.Value = 42;

            // Assert
            syncVar.Value.Should().Be(42, "value should be set correctly");

            _output.WriteLine("✓ Test passed: Host sync var value updated correctly");
        }

        [Fact]
        public async Task HostSyncVar_OnValueChanged_FiresOnChange()
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

            var syncVar = _fixture.Fixture.Client!.CreateHostSyncVar("EventTest", 0);
            
            int? oldValue = null;
            int? newValue = null;
            syncVar.OnValueChanged += (old, @new) =>
            {
                oldValue = old;
                newValue = @new;
                _output.WriteLine($"Value changed: {old} -> {@new}");
            };

            // Act
            syncVar.Value = 100;

            // Assert
            oldValue.Should().Be(0, "old value should be initial value");
            newValue.Should().Be(100, "new value should be set value");

            _output.WriteLine("✓ Test passed: OnValueChanged fired correctly");
        }

        [Fact]
        public async Task HostSyncVar_ComplexObject_SynchronizesCorrectly()
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

            var defaultState = new GameState { Round = 0, Score = 0, GameMode = "Idle" };
            var syncVar = _fixture.Fixture.Client!.CreateHostSyncVar("GameState", defaultState);

            // Act
            var newState = new GameState { Round = 3, Score = 1500, GameMode = "Competitive" };
            syncVar.Value = newState;

            // Assert
            syncVar.Value.Should().NotBeNull();
            syncVar.Value.Round.Should().Be(3);
            syncVar.Value.Score.Should().Be(1500);
            syncVar.Value.GameMode.Should().Be("Competitive");

            _output.WriteLine("✓ Test passed: Complex object synchronized correctly");
        }

        [Fact]
        public async Task HostSyncVar_IsHost_CanWrite()
        {
            // Arrange
            _output.WriteLine("=== Test: Host can write to HostSyncVar ===");
            
            if (!_fixture.Fixture.IsInitialized)
            {
                _output.WriteLine("Steam not initialized - skipping test");
                return;
            }

            var lobbyId = await _fixture.Fixture.CreateLobbyAsync(4);
            await Task.Delay(200);

            // Host creates a sync var
            _fixture.Fixture.Client!.IsHost.Should().BeTrue("we should be the host");

            var syncVar = _fixture.Fixture.Client!.CreateHostSyncVar("HostWriteTest", "initial");

            // Act - Host writes
            syncVar.Value = "updated";

            // Assert
            syncVar.Value.Should().Be("updated", "host should be able to write");

            _output.WriteLine("✓ Test passed: Host can write to HostSyncVar");
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
    /// Test class for complex object synchronization.
    /// </summary>
    public class GameState
    {
        public int Round { get; set; }
        public int Score { get; set; }
        public string GameMode { get; set; } = string.Empty;
    }
}
