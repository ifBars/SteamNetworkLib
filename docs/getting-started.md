# Getting Started

This guide walks you through setting up SteamNetworkLib in a Schedule 1 MelonLoader mod and implementing the minimal loop.

## Installation

### Prerequisites

1. MelonLoader installed on Schedule 1
2. Steam client running
3. Schedule 1 launched through Steam
4. Basic C# and MelonLoader modding knowledge
5. Visual Studio or VS Code

### Add SteamNetworkLib to Your Mod Project

Target .NET Standard 2.1 (works for Mono and Il2Cpp) and reference SteamNetworkLib:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyTitle>YourScheduleOneMod</AssemblyTitle>
    <AssemblyVersion>1.0.0</AssemblyVersion>
  </PropertyGroup>
</Project>
```

Add references to:
- MelonLoader.dll
- UnityEngine.dll
- Assembly-CSharp.dll
- SteamNetworkLib.dll

Download the matching SteamNetworkLib DLL from the [GitHub releases page](https://github.com/ifBars/SteamNetworkLib/releases), keep that DLL in your mod project or local dependency folder, and reference the runtime-specific binary:

- Mono mods/configurations must reference the Mono release DLL
- Il2Cpp mods/configurations must reference the Il2Cpp release DLL

Do not mix these references across build targets, and do not point your mod project at a sibling `..\SteamNetworkLib\bin\...` source checkout.

If your mod has separate Mono and Il2Cpp configurations, reference the downloaded DLLs conditionally in your `.csproj`:

```xml
<ItemGroup Condition="'$(Configuration)'=='Mono' or '$(Configuration)'=='MonoDebug'">
  <Reference Include="SteamNetworkLib">
    <HintPath>libs\SteamNetworkLib\Mono\SteamNetworkLib.dll</HintPath>
  </Reference>
</ItemGroup>

<ItemGroup Condition="'$(Configuration)'=='Il2cpp' or '$(Configuration)'=='Il2cppDebug'">
  <Reference Include="SteamNetworkLib">
    <HintPath>libs\SteamNetworkLib\Il2cpp\SteamNetworkLib.dll</HintPath>
  </Reference>
</ItemGroup>
```

## When to initialize

SteamNetworkLib requires the game process to have Steamworks attached and initialized. In Schedule One mods, that is not always true during `OnInitializeMelon()`, and it is common for users to launch in a way where Steamworks is unavailable. Treat networking as an optional capability until initialization succeeds.

Recommended lifecycle:

- Create the `SteamNetworkClient` in `OnInitializeMelon()` or when your network feature starts.
- Attempt initialization after the menu or first relevant scene has loaded, then retry on a short timer if Steamworks is not ready.
- Keep your mod's single-player/local behavior working when initialization fails.
- Call `ProcessIncomingMessages()` every frame only after the client is initialized.
- Dispose the client in `OnDeinitializeMelon()`.

Use `TryInitialize()` for normal consumer mods. Use `Initialize()` only when networking is mandatory and a thrown exception should fail the feature immediately.

## Minimal mod setup

```csharp
using MelonLoader;
using SteamNetworkLib;
using SteamNetworkLib.Core;
using SteamNetworkLib.Exceptions;
using UnityEngine;

public class YourScheduleOneModMain : MelonMod
{
    private SteamNetworkClient client;
    private float nextNetworkInitAttempt;
    private bool multiplayerAvailable;

    public override void OnInitializeMelon()
    {
        // Optional: configure network rules (relay, session policy, channels)
        var rules = new NetworkRules
        {
            EnableRelay = true,
            AcceptOnlyFriends = false
        };

        client = new SteamNetworkClient(rules);
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        if (sceneName == "Menu")
        {
            TryInitializeNetworking();
        }
    }

    public override void OnUpdate()
    {
        if (!multiplayerAvailable && Time.realtimeSinceStartup >= nextNetworkInitAttempt)
        {
            TryInitializeNetworking();
        }

        if (multiplayerAvailable)
        {
            client.ProcessIncomingMessages();
        }
    }

    public override void OnDeinitializeMelon()
    {
        client?.Dispose();
    }

    private void TryInitializeNetworking()
    {
        if (client.IsInitialized)
        {
            multiplayerAvailable = true;
            return;
        }

        if (client.TryInitialize(out var error))
        {
            multiplayerAvailable = true;
            client.OnLobbyCreated += (s, e) => MelonLogger.Msg($"Lobby: {e.Lobby.LobbyId64}");
            MelonLogger.Msg("SteamNetworkLib initialized.");
            return;
        }

        multiplayerAvailable = false;
        nextNetworkInitAttempt = Time.realtimeSinceStartup + 2f;
        MelonLogger.Warning($"Steam networking unavailable, retrying later: {error?.Message}");
    }
}
```

If multiplayer is optional, guard every sync/send path with your own `multiplayerAvailable` flag and keep local logic active. Do not call lobby, member data, SyncVar, or P2P methods before initialization succeeds.

### Optional runner helper

For the common retry-and-update loop, use `SteamNetworkClientRunner`:

```csharp
private SteamNetworkClient client;
private SteamNetworkClientRunner runner;

public override void OnInitializeMelon()
{
    client = new SteamNetworkClient(new NetworkRules
    {
        EnableRelay = true,
        AcceptOnlyFriends = false
    });

    runner = new SteamNetworkClientRunner(client);
    runner.OnInitialized += RegisterNetworkHandlers;
    runner.OnInitializationFailed += error =>
    {
        MelonLogger.Warning($"Steam networking unavailable: {error?.Message}");
    };
}

public override void OnUpdate()
{
    runner.Tick();
}

public override void OnDeinitializeMelon()
{
    runner.Dispose();
}
```

The runner retries initialization on a timer and calls `ProcessIncomingMessages()` after networking is available. Keep local behavior active while `runner.IsAvailable` is false.

## Working with Steam IDs

SteamNetworkLib still exposes the native `CSteamID` for Steamworks interop, but common identity lookups also have runtime-neutral helpers:

```csharp
if (client.TryGetHostMember(out var host))
{
    MelonLogger.Msg($"Host: {host.DisplayName} ({host.SteamId64})");
}

ulong localPlayer = client.LocalPlayerId64;
ulong hostPlayer = client.HostPlayerId64;

foreach (var member in client.GetRemoteMembers())
{
    MelonLogger.Msg($"Remote member: {member.DisplayName} ({member.SteamIdString})");
}
```

Prefer the `ulong` properties for config files, JSON payloads, dictionaries, and logs. Convert back to `CSteamID` only when you call an API that requires the Steamworks type.

## Best Practices

- **Use unique prefixes** for your mod's data keys to avoid collisions with other mods. See [Data Synchronization](data-synchronization.md#important-use-unique-prefixes) for details.
- **Fail open for optional multiplayer**. If Steamworks is unavailable, log once or retry quietly and keep single-player behavior working.
- **Retry instead of crashing** when Steamworks is not ready. `TryInitialize()` is intended for this path.

From here, pick the guide you need next:

- Lobby Management: create/join/leave/invite
- Data Synchronization: lobby and member data
- P2P Messaging: typed messages, files, channels
- Patterns from Real Mods: common adapter, snapshot, request, and polling shapes
- Events and Errors: event model and exceptions
- Recipes: copy-paste snippets for common tasks
