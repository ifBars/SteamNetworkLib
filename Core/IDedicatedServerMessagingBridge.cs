using System;

namespace SteamNetworkLib.Core
{
    internal interface IDedicatedServerMessagingBridge : IDisposable
    {
        event Action<string, string>? MessageReceived;

        event Action? EndpointReady;

        bool IsDedicatedContextLikely { get; }

        bool TrySendToServer(string command, string payload);
    }
}
