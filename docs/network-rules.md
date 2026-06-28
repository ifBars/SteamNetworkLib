# Network Rules

SteamNetworkLib exposes a lightweight Network Rules system to control Steamworks behavior without touching low-level APIs.

## Quick Start
```csharp
using SteamNetworkLib;
using SteamNetworkLib.Core;

var rules = new NetworkRules
{
    EnableRelay = true,          // Use Steam relay for NAT traversal
    AcceptOnlyFriends = false,   // Accept sessions from anyone
};

var client = new SteamNetworkClient(rules);
if (!client.TryInitialize())
{
    // Steamworks is not ready or unavailable. Retry later if multiplayer is optional.
    return;
}
```

## Message Policy
Choose channel and send type per message (e.g., unreliable for streams):
```csharp
rules.MessagePolicy = msg =>
{
    if (msg is StreamMessage s)
        return (channel: 1, sendType: s.RecommendedSendType);
    return (channel: 0, sendType: rules.DefaultSendType);
};
```

## Runtime Updates
Rules can be swapped at runtime; global settings like relay are applied immediately:
```csharp
rules.EnableRelay = false;
client.UpdateNetworkRules(rules);
```

## Incoming Channel Range
Incoming packet polling scans a channel range on both Mono and IL2CPP builds:
- `MinReceiveChannel` (default 0)
- `MaxReceiveChannel` (default 3)

Tune these if you segment traffic across channels.
