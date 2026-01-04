# Getting Started

This guide walks you through setting up SteamNetworkLib in a Unity game mod using MelonLoader and implementing the minimal loop.

## Installation

### Prerequisites

1. MelonLoader installed on the target Unity game
2. Steam client running
3. Unity game with Steam integration
4. Basic C# and MelonLoader modding knowledge
5. Visual Studio or VS Code

### Add SteamNetworkLib to Your Mod Project

Target .NET Standard 2.1 (works for Mono and Il2Cpp) and reference SteamNetworkLib:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyTitle>YourAwesomeMod</AssemblyTitle>
    <AssemblyVersion>1.0.0</AssemblyVersion>
  </PropertyGroup>
</Project>
```

Add references to:
- MelonLoader.dll
- UnityEngine.dll
- Assembly-CSharp.dll
- SteamNetworkLib.dll

## Minimal mod setup

```csharp
using MelonLoader;
using SteamNetworkLib;

public class YourAwesomeModMain : MelonMod
{
    private SteamNetworkClient client;

    public override void OnInitializeMelon()
    {
        // Optional: configure network rules (relay, session policy, channels)
        var rules = new SteamNetworkLib.Core.NetworkRules
        {
            EnableRelay = true,
            AcceptOnlyFriends = false
        };

        client = new SteamNetworkClient(rules);
        if (client.Initialize())
        {
            // Optional: subscribe to events
            client.OnLobbyCreated += (s, e) => MelonLogger.Msg($"Lobby: {e.Lobby.LobbyId}");
        }
    }

    public override void OnUpdate()
    {
        client?.ProcessIncomingMessages();
    }

    public override void OnDeinitializeMelon()
    {
        client?.Dispose();
    }
}
```

## Best Practices

- **Use unique prefixes** for your mod's data keys to avoid collisions with other mods. See [Data Synchronization](data-synchronization.md#important-use-unique-prefixes) for details.

From here, pick the guide you need next:

- Lobby Management: create/join/leave/invite
- Data Synchronization: lobby and member data
- P2P Messaging: typed messages, files, channels
- Events and Errors: event model and exceptions
- Recipes: copy-paste snippets for common tasks
