using System;
using FluentAssertions;
using SteamNetworkLib.Sync;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    /// <summary>
    /// Unit tests for NetworkSyncOptions configuration class.
    /// </summary>
    public class NetworkSyncOptionsTests
    {
        [Fact]
        public void NetworkSyncOptions_DefaultConstructor_SetsDefaultValues()
        {
            // Arrange & Act
            var options = new NetworkSyncOptions();

            // Assert
            options.AutoSync.Should().BeTrue("auto sync is enabled by default");
            options.SyncOnPlayerJoin.Should().BeTrue("sync on player join is enabled by default");
            options.MaxSyncsPerSecond.Should().Be(0, "no rate limiting by default");
            options.ThrowOnValidationError.Should().BeFalse("validation errors are logged by default");
            options.WarnOnIgnoredWrites.Should().BeFalse("ignored write warnings are off by default");
            options.Serializer.Should().BeNull("default serializer is null (uses JsonSyncSerializer)");
            options.KeyPrefix.Should().BeNull("no key prefix by default");
        }

        [Fact]
        public void NetworkSyncOptions_AutoSync_CanBeDisabled()
        {
            // Arrange
            var options = new NetworkSyncOptions
            {
                AutoSync = false
            };

            // Assert
            options.AutoSync.Should().BeFalse();
        }

        [Fact]
        public void NetworkSyncOptions_SyncOnPlayerJoin_CanBeDisabled()
        {
            // Arrange
            var options = new NetworkSyncOptions
            {
                SyncOnPlayerJoin = false
            };

            // Assert
            options.SyncOnPlayerJoin.Should().BeFalse();
        }

        [Fact]
        public void NetworkSyncOptions_MaxSyncsPerSecond_CanBeSet()
        {
            // Arrange
            var options = new NetworkSyncOptions
            {
                MaxSyncsPerSecond = 30
            };

            // Assert
            options.MaxSyncsPerSecond.Should().Be(30);
        }

        [Fact]
        public void NetworkSyncOptions_ThrowOnValidationError_CanBeEnabled()
        {
            // Arrange
            var options = new NetworkSyncOptions
            {
                ThrowOnValidationError = true
            };

            // Assert
            options.ThrowOnValidationError.Should().BeTrue();
        }

        [Fact]
        public void NetworkSyncOptions_WarnOnIgnoredWrites_CanBeEnabled()
        {
            // Arrange
            var options = new NetworkSyncOptions
            {
                WarnOnIgnoredWrites = true
            };

            // Assert
            options.WarnOnIgnoredWrites.Should().BeTrue();
        }

        [Fact]
        public void NetworkSyncOptions_KeyPrefix_CanBeSet()
        {
            // Arrange
            var options = new NetworkSyncOptions
            {
                KeyPrefix = "MyMod_"
            };

            // Assert
            options.KeyPrefix.Should().Be("MyMod_");
        }

        [Fact]
        public void NetworkSyncOptions_Serializer_CanBeSetToCustom()
        {
            // Arrange
            var customSerializer = new JsonSyncSerializer();
            var options = new NetworkSyncOptions
            {
                Serializer = customSerializer
            };

            // Assert
            options.Serializer.Should().BeSameAs(customSerializer);
        }

        [Fact]
        public void NetworkSyncOptions_AllProperties_CanBeSetTogether()
        {
            // Arrange & Act
            var options = new NetworkSyncOptions
            {
                AutoSync = false,
                SyncOnPlayerJoin = false,
                MaxSyncsPerSecond = 60,
                ThrowOnValidationError = true,
                WarnOnIgnoredWrites = true,
                KeyPrefix = "TestMod_",
                Serializer = new JsonSyncSerializer()
            };

            // Assert
            options.AutoSync.Should().BeFalse();
            options.SyncOnPlayerJoin.Should().BeFalse();
            options.MaxSyncsPerSecond.Should().Be(60);
            options.ThrowOnValidationError.Should().BeTrue();
            options.WarnOnIgnoredWrites.Should().BeTrue();
            options.KeyPrefix.Should().Be("TestMod_");
            options.Serializer.Should().NotBeNull();
        }
    }
}
