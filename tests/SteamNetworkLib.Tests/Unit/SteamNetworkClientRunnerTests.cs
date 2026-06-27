using System;
using FluentAssertions;
using SteamNetworkLib.Exceptions;
using SteamNetworkLib.Utilities;
using Xunit;

namespace SteamNetworkLib.Tests.Unit
{
    public class SteamNetworkClientRunnerTests
    {
        [Fact]
        public void Tick_WhenInitializationSucceeds_RaisesInitialized()
        {
            var now = new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc);
            var client = new FakeLifecycleClient();
            client.InitializeResults.Enqueue(true);
            var runner = new SteamNetworkClientRunner(client, null, () => now);
            var initializedCount = 0;
            runner.OnInitialized += () => initializedCount++;

            var available = runner.Tick();

            available.Should().BeTrue();
            initializedCount.Should().Be(1);
            client.InitializeAttempts.Should().Be(1);
        }

        [Fact]
        public void Tick_WhenInitializationFails_WaitsForRetryInterval()
        {
            var now = new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc);
            var client = new FakeLifecycleClient();
            client.InitializeResults.Enqueue(false);
            client.InitializeResults.Enqueue(true);
            var runner = new SteamNetworkClientRunner(
                client,
                new SteamNetworkClientRunnerOptions { RetryInterval = TimeSpan.FromSeconds(2) },
                () => now);
            var failureCount = 0;
            runner.OnInitializationFailed += _ => failureCount++;

            runner.Tick().Should().BeFalse();
            runner.Tick().Should().BeFalse();
            client.InitializeAttempts.Should().Be(1);

            now = now.AddSeconds(2);
            runner.Tick().Should().BeTrue();

            client.InitializeAttempts.Should().Be(2);
            failureCount.Should().Be(1);
        }

        [Fact]
        public void Tick_WhenInitialized_ProcessesIncomingMessages()
        {
            var client = new FakeLifecycleClient { IsInitializedValue = true };
            var runner = new SteamNetworkClientRunner(client);

            runner.Tick().Should().BeTrue();

            client.ProcessCalls.Should().Be(1);
        }

        [Fact]
        public void Tick_WhenProcessingThrows_RaisesProcessingFailed()
        {
            var client = new FakeLifecycleClient
            {
                IsInitializedValue = true,
                ProcessingException = new InvalidOperationException("boom")
            };
            var runner = new SteamNetworkClientRunner(client);
            Exception? observed = null;
            runner.OnProcessingFailed += ex => observed = ex;

            runner.Tick().Should().BeTrue();

            observed.Should().BeSameAs(client.ProcessingException);
        }

        [Fact]
        public void Dispose_DisposesWrappedClient()
        {
            var client = new FakeLifecycleClient();
            var runner = new SteamNetworkClientRunner(client);

            runner.Dispose();

            client.Disposed.Should().BeTrue();
            Action tick = () => runner.Tick();
            tick.Should().Throw<ObjectDisposedException>();
        }

        private sealed class FakeLifecycleClient : ISteamNetworkClientLifecycle
        {
            public System.Collections.Generic.Queue<bool> InitializeResults { get; } =
                new System.Collections.Generic.Queue<bool>();

            public bool IsInitializedValue { get; set; }
            public int InitializeAttempts { get; private set; }
            public int ProcessCalls { get; private set; }
            public bool Disposed { get; private set; }
            public Exception? ProcessingException { get; set; }

            public bool IsInitialized => IsInitializedValue;

            public bool TryInitialize(out SteamNetworkException? error)
            {
                InitializeAttempts++;
                error = null;

                var result = InitializeResults.Count == 0 || InitializeResults.Dequeue();
                IsInitializedValue = result;
                return result;
            }

            public void ProcessIncomingMessages()
            {
                ProcessCalls++;
                if (ProcessingException != null)
                {
                    throw ProcessingException;
                }
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
