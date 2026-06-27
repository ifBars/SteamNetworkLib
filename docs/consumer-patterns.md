# Patterns from Real Mods

SteamNetworkLib consumers tend to solve the same networking problems in their own adapters. These patterns are based on public/local mods that already integrate SteamNetworkLib, including optional config sync, label/state sync, voice transport, client requests, and host-owned transaction flows.

Use this page as a decision guide before choosing raw lobby data, SyncVars, or P2P messages.

## Repeated patterns

| Pattern | Seen in mods like | Recommended SteamNetworkLib shape |
| --- | --- | --- |
| Optional multiplayer adapter with local fallback | BetterStacksF, AutoRestock, ConsoleForAll | Initialize with retry, keep local behavior active, call `ProcessIncomingMessages()` only after ready |
| Host-owned config or state snapshot | BetterStacksF, SimpleLabels, Time Never Stops | `HostSyncVar<T>` for typed state, raw lobby data for legacy strings |
| Client asks host to perform an action | ConsoleForAll, OTC-style host transactions, restock flows | Typed P2P request/response with a `RequestId`, timeout, and host validation |
| Full snapshot plus small deltas | SimpleLabels-style label state | Host publishes full state; P2P or SyncVar deltas update the hot path |
| Raw string compatibility payloads | OTC-style pipe-delimited state | `NetworkSyncOptions.Serializer` with a raw string serializer when exact formatting matters |
| Member-data polling fallback | OTC and label sync request flows | Poll `Refresh()`/`GetAllValues()` when Steam lobby callbacks are unreliable in the target runtime |
| Custom transport wrapper | S1 Voice Chat | Hide SteamNetworkLib behind a small mod-local interface so audio/gameplay code is not coupled to transport details |

## Optional networking adapter

Many mods should treat SteamNetworkLib as optional. A user may launch without Steamworks ready, may be playing solo, or may have a missing/incorrect DLL. Keep your core mod logic usable and swap in the network adapter when initialization succeeds.

```csharp
public interface IModNetworkAdapter
{
    bool IsAvailable { get; }
    void Tick();
    void Dispose();
}

public sealed class LocalNetworkAdapter : IModNetworkAdapter
{
    public bool IsAvailable => false;
    public void Tick() { }
    public void Dispose() { }
}

public sealed class SteamNetworkAdapter : IModNetworkAdapter
{
    private readonly SteamNetworkClient client;

    public SteamNetworkAdapter(SteamNetworkClient client)
    {
        this.client = client;
    }

    public bool IsAvailable => client.IsInitialized;

    public void Tick()
    {
        if (client.IsInitialized)
        {
            client.ProcessIncomingMessages();
        }
    }

    public void Dispose()
    {
        client.Dispose();
    }
}
```

Keep retries outside gameplay logic:

```csharp
private IModNetworkAdapter network = new LocalNetworkAdapter();
private SteamNetworkClient steamClient = new SteamNetworkClient();
private float nextSteamRetry;

public override void OnUpdate()
{
    if (!steamClient.IsInitialized && Time.realtimeSinceStartup >= nextSteamRetry)
    {
        if (steamClient.TryInitialize(out var error))
        {
            network.Dispose();
            network = new SteamNetworkAdapter(steamClient);
            RegisterNetworkHandlers(steamClient);
        }
        else
        {
            nextSteamRetry = Time.realtimeSinceStartup + 2f;
            MelonLogger.Warning($"Steam networking unavailable: {error?.Message}");
        }
    }

    network.Tick();
}
```

## Host snapshot plus targeted updates

For durable shared state, prefer a host-owned snapshot and explicit deltas:

- The host owns the authoritative full state.
- New or resyncing clients read the snapshot.
- Hot interactions send small messages or small SyncVar updates.
- Clients can request a resync with per-client member data or a P2P request.

This works well for labels, host config, time settings, global toggles, and other state where late joiners need a complete view.

```csharp
var options = new NetworkSyncOptions { KeyPrefix = "MyMod_" };
var state = client.CreateHostSyncVar("State", new MyStateSnapshot(), options);

if (client.IsHost)
{
    state.Value = BuildSnapshotFromLocalState();
}

state.OnValueChanged += (oldValue, newValue) =>
{
    ApplySnapshot(newValue);
};
```

Do not sync Unity objects or live game references. Sync stable IDs, quantities, coordinates, and versioned DTOs, then resolve runtime objects locally.

## Client request, host decision

When a client wants to mutate shared game state, send intent to the host. The host validates the current state and responds with the result. Do not let clients directly sync host-owned final state.

Request payloads should include:

- A stable request ID or use the request/response helper.
- Actor identity if the host needs to verify ownership.
- Stable target identifiers such as item IDs, slot IDs, property IDs, or entity GUIDs.
- Small primitive values only.

Host responses should include:

- Whether the request succeeded.
- Any stable result ID the client needs.
- The host-approved final value, not just an acknowledgement.
- A short error string for debugging or UI.

## Polling fallback

Steam lobby data callbacks may not fire reliably in every runtime/mod stack. If the value is important and cheap to read, combine event handling with a slow poll:

```csharp
private float nextRefresh;

public override void OnUpdate()
{
    client.ProcessIncomingMessages();

    if (Time.realtimeSinceStartup < nextRefresh)
    {
        return;
    }

    nextRefresh = Time.realtimeSinceStartup + 2f;
    readyState.Refresh();
    foreach (var pair in readyState.GetAllValues())
    {
        ApplyReadyState(pair.Key, pair.Value);
    }
}
```

Poll for correctness, not for animation. High-frequency state belongs in P2P packets or local prediction, not lobby/member data.

## What not to sync

Avoid syncing these directly:

- `GameObject`, `Transform`, `MonoBehaviour`, or `ScriptableObject` instances.
- Inventory item instances, storage slot objects, NPC/player controller objects, or scene references.
- Values that can be recomputed locally from a stable ID.
- Rapid per-frame state through lobby/member data.
- Secrets, auth tokens, local paths, or user-specific private settings.

Prefer these instead:

- Stable item IDs, property IDs, entity GUIDs, slot indexes, coordinates, and primitive state.
- Host-owned snapshots for durable shared state.
- Client-owned SyncVars for ready status, preferences, and small per-player flags.
- P2P messages for actions, deltas, voice/audio packets, and large/fast data.
