# Beginner Quickstart

This path focuses on the simplest way to get multiplayer features working with minimal code.

## One-time setup
```csharp
using MelonLoader;
using SteamNetworkLib;
using SteamNetworkLib.Core;

public class MyFirstMultiplayerMod : MelonMod
{
    private SteamNetworkClient client;

    public override void OnInitializeMelon()
    {
        // Friendly defaults: relay on, accept any session
        var rules = new NetworkRules { EnableRelay = true, AcceptOnlyFriends = false };
        client = new SteamNetworkClient(rules);

        if (client.Initialize())
        {
            // Log any text messages we receive (high-level API)
            client.RegisterMessageHandler<TextMessage>((msg, sender) =>
                MelonLogger.Msg($"{sender}: {msg.Content}"));
        }
    }

    public override void OnDeinitializeMelon()
    {
        client?.Dispose();
    }
}
```

## Sending a message
```csharp
// To a single player
await client.SendMessageToPlayerAsync(targetId, new TextMessage { Content = "Hello!" });

// To everyone in the lobby
await client.BroadcastMessageAsync(new TextMessage { Content = "Welcome!" });
```

## Tips
- Call `CreateLobbyAsync(...)` or `JoinLobbyAsync(lobbyId)` via `client.LobbyManager` when ready.
- In your mod's update loop, call `client.ProcessIncomingMessages()`.
- Use `NetworkRules.MessagePolicy` to route different message types to channels automatically.
- Keep large data chunked under `client.P2PManager.MaxPacketSize`.
