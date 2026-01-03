using System;
using FluentAssertions;
using SteamNetworkLib.Core;
using SteamNetworkLib.Models;
using Steamworks;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    /// <summary>
    /// Unit tests for NetworkRules configuration class.
    /// </summary>
    public class NetworkRulesTests
    {
        [Fact]
        public void NetworkRules_DefaultConstructor_SetsDefaultValues()
        {
            // Arrange & Act
            var rules = new NetworkRules();

            // Assert
            rules.EnableRelay.Should().BeTrue("relay is enabled by default for NAT traversal");
            rules.DefaultSendType.Should().Be(EP2PSend.k_EP2PSendReliable, "reliable send is default");
            rules.MinReceiveChannel.Should().Be(0, "min channel is 0");
            rules.MaxReceiveChannel.Should().Be(3, "max channel is 3");
            rules.AcceptOnlyFriends.Should().BeFalse("accept all connections by default");
            rules.MessagePolicy.Should().BeNull("no custom message policy by default");
        }

        [Fact]
        public void NetworkRules_EnableRelay_CanBeDisabled()
        {
            // Arrange
            var rules = new NetworkRules
            {
                EnableRelay = false
            };

            // Assert
            rules.EnableRelay.Should().BeFalse();
        }

        [Fact]
        public void NetworkRules_DefaultSendType_CanBeSetToUnreliable()
        {
            // Arrange
            var rules = new NetworkRules
            {
                DefaultSendType = EP2PSend.k_EP2PSendUnreliable
            };

            // Assert
            rules.DefaultSendType.Should().Be(EP2PSend.k_EP2PSendUnreliable);
        }

        [Fact]
        public void NetworkRules_DefaultSendType_CanBeSetToReliableWithBuffering()
        {
            // Arrange
            var rules = new NetworkRules
            {
                DefaultSendType = EP2PSend.k_EP2PSendReliableWithBuffering
            };

            // Assert
            rules.DefaultSendType.Should().Be(EP2PSend.k_EP2PSendReliableWithBuffering);
        }

        [Fact]
        public void NetworkRules_MinReceiveChannel_CanBeSet()
        {
            // Arrange
            var rules = new NetworkRules
            {
                MinReceiveChannel = 1
            };

            // Assert
            rules.MinReceiveChannel.Should().Be(1);
        }

        [Fact]
        public void NetworkRules_MaxReceiveChannel_CanBeSet()
        {
            // Arrange
            var rules = new NetworkRules
            {
                MaxReceiveChannel = 10
            };

            // Assert
            rules.MaxReceiveChannel.Should().Be(10);
        }

        [Fact]
        public void NetworkRules_ChannelRange_CanBeConfigured()
        {
            // Arrange
            var rules = new NetworkRules
            {
                MinReceiveChannel = 2,
                MaxReceiveChannel = 5
            };

            // Assert
            rules.MinReceiveChannel.Should().Be(2);
            rules.MaxReceiveChannel.Should().Be(5);
            rules.MaxReceiveChannel.Should().BeGreaterThan(rules.MinReceiveChannel);
        }

        [Fact]
        public void NetworkRules_AcceptOnlyFriends_CanBeEnabled()
        {
            // Arrange
            var rules = new NetworkRules
            {
                AcceptOnlyFriends = true
            };

            // Assert
            rules.AcceptOnlyFriends.Should().BeTrue();
        }

        [Fact]
        public void NetworkRules_MessagePolicy_CanBeSetToCustomFunction()
        {
            // Arrange
            Func<P2PMessage, (int channel, EP2PSend sendType)> customPolicy = (msg) =>
            {
                if (msg is TextMessage)
                    return (0, EP2PSend.k_EP2PSendReliable);
                else
                    return (1, EP2PSend.k_EP2PSendUnreliable);
            };

            var rules = new NetworkRules
            {
                MessagePolicy = customPolicy
            };

            // Assert
            rules.MessagePolicy.Should().BeSameAs(customPolicy);
        }

        [Fact]
        public void NetworkRules_MessagePolicy_ReturnsCorrectChannelForTextMessage()
        {
            // Arrange
            var rules = new NetworkRules
            {
                MessagePolicy = (msg) =>
                {
                    if (msg is TextMessage)
                        return (5, EP2PSend.k_EP2PSendReliable);
                    else
                        return (0, EP2PSend.k_EP2PSendUnreliable);
                }
            };

            var textMessage = new TextMessage { Content = "test" };

            // Act
            var (channel, sendType) = rules.MessagePolicy!(textMessage);

            // Assert
            channel.Should().Be(5);
            sendType.Should().Be(EP2PSend.k_EP2PSendReliable);
        }

        [Fact]
        public void NetworkRules_MessagePolicy_ReturnsCorrectChannelForDataSyncMessage()
        {
            // Arrange
            var rules = new NetworkRules
            {
                MessagePolicy = (msg) =>
                {
                    if (msg is DataSyncMessage)
                        return (2, EP2PSend.k_EP2PSendReliableWithBuffering);
                    else
                        return (0, EP2PSend.k_EP2PSendUnreliable);
                }
            };

            var dataSyncMessage = new DataSyncMessage { Key = "test", Value = "value" };

            // Act
            var (channel, sendType) = rules.MessagePolicy!(dataSyncMessage);

            // Assert
            channel.Should().Be(2);
            sendType.Should().Be(EP2PSend.k_EP2PSendReliableWithBuffering);
        }

        [Fact]
        public void NetworkRules_MessagePolicy_CanPrioritizeByMessageType()
        {
            // Arrange
            var rules = new NetworkRules
            {
                MessagePolicy = (msg) => msg.MessageType switch
                {
                    "TEXT" => (0, EP2PSend.k_EP2PSendReliable),
                    "DATA_SYNC" => (1, EP2PSend.k_EP2PSendReliableWithBuffering),
                    "HEARTBEAT" => (2, EP2PSend.k_EP2PSendUnreliable),
                    _ => (0, EP2PSend.k_EP2PSendReliable)
                }
            };

            // Act & Assert
            var textResult = rules.MessagePolicy!(new TextMessage());
            textResult.channel.Should().Be(0);
            textResult.sendType.Should().Be(EP2PSend.k_EP2PSendReliable);

            var dataSyncResult = rules.MessagePolicy!(new DataSyncMessage());
            dataSyncResult.channel.Should().Be(1);
            dataSyncResult.sendType.Should().Be(EP2PSend.k_EP2PSendReliableWithBuffering);

            var heartbeatResult = rules.MessagePolicy!(new HeartbeatMessage());
            heartbeatResult.channel.Should().Be(2);
            heartbeatResult.sendType.Should().Be(EP2PSend.k_EP2PSendUnreliable);
        }

        [Fact]
        public void NetworkRules_AllProperties_CanBeSetTogether()
        {
            // Arrange & Act
            var rules = new NetworkRules
            {
                EnableRelay = false,
                DefaultSendType = EP2PSend.k_EP2PSendUnreliable,
                MinReceiveChannel = 1,
                MaxReceiveChannel = 5,
                AcceptOnlyFriends = true,
                MessagePolicy = (msg) => (0, EP2PSend.k_EP2PSendReliable)
            };

            // Assert
            rules.EnableRelay.Should().BeFalse();
            rules.DefaultSendType.Should().Be(EP2PSend.k_EP2PSendUnreliable);
            rules.MinReceiveChannel.Should().Be(1);
            rules.MaxReceiveChannel.Should().Be(5);
            rules.AcceptOnlyFriends.Should().BeTrue();
            rules.MessagePolicy.Should().NotBeNull();
        }
    }
}
