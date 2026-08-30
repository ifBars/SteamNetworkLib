# Making Mods Multiplayer Compatible

SteamNetworkLib helps Schedule 1 mods share state through Steam lobby data, member data, and P2P messages. The hardest part is not the API call; it is deciding which machine owns each piece of state.

## Authority Model

Most mods should use a host/client model:

| Owner | Good for | SteamNetworkLib API |
|-------|----------|---------------------|
| Host | Shared gameplay state, economy decisions, spawned world changes, round or job phase, generated IDs, save-affecting choices | `HostSyncVar<T>`, lobby data, host-broadcast typed P2P |
| Local client | Ready status, loadout selection, cosmetics, UI preferences, short-lived player intent | `ClientSyncVar<T>`, member data, client-to-host typed P2P |
| Everyone independently | Visual-only state, debug UI, previews, cached lookups, local config | Do not sync |

When in doubt, let the host own state that can change another player's world or save data. Clients can request actions, but the host should validate and publish the accepted result.

## What Should Be Synced

Sync the smallest durable facts that remote players need to agree on:

- Host-selected config that affects all players.
- A mission, job, minigame, or event phase.
- IDs for host-created entities that clients need to reference.
- Per-player ready status, selected role, or current cosmetic choice.
- A final accepted result after the host validates a client request.

Prefer syncing identifiers and compact DTOs over entire runtime objects. For example, sync `"og-kush"` or a serializable `ProductSelection` DTO, not a live `ProductDefinition`, `GameObject`, `NPC`, `Transform`, or UI component.

## What Should Not Be Synced

Do not sync state that is already local, transient, or too expensive:

- Unity objects, components, transforms, materials, prefabs, or scene references.
- UI open/closed state unless another player must react to it.
- Large config blobs on every frame.
- Save data owned by the base game unless the host is intentionally publishing a small derived value.
- High-frequency positions or input every update without rate limiting.
- Secrets, local file paths, tokens, private settings, or anything from a user's machine.

If the value changes often, ask whether it needs to be exact. Many mod features only need event messages such as `Started`, `Accepted`, `Completed`, or `Cancelled`.

## Picking the API

Use the API by ownership and payload shape:

| Need | Recommended API |
|------|-----------------|
| One shared host-owned value | `client.CreateHostSyncVar("Key", defaultValue, options)` |
| One value per player | `client.CreateClientSyncVar("Key", defaultValue, options)` |
| A command, request, or event | `client.SendMessageToPlayerAsync(targetId, new MyMessage(payload))` or `client.BroadcastMessageAsync(new MyMessage(payload))`, where `MyMessage` inherits `TypedP2PMessage<TPayload>` |
| Large data or files | `SendLargeDataToPlayerAsync()` / `FileTransferMessage` |
| Mod/version compatibility | `SyncModDataWithAllPlayers()`, `IsModDataCompatible()`, `OnVersionMismatch` |
| Small raw string flags | Lobby/member data, with a unique key prefix |

SyncVars are the default for small typed state. In normal use, `CreateHostSyncVar<T>()` and `CreateClientSyncVar<T>()` infer `T` from `defaultValue`; specify the type argument only when inference is not clear. Typed P2P is better for request/response flows where the receiver needs to validate a payload before mutating host-owned state.

## Example: Host-Owned Shared State

```csharp
private SteamNetworkClient client = new SteamNetworkClient();
private HostSyncVar<int>? eventPhase;

private void CreateSyncVars()
{
    var options = new NetworkSyncOptions { KeyPrefix = "MyMod_" };

    eventPhase = client.CreateHostSyncVar("EventPhase", 0, options);
    eventPhase.OnValueChanged += (_, newPhase) =>
    {
        ApplyEventPhase(newPhase);
    };
}

private void StartEvent()
{
    if (!client.IsHost || eventPhase == null)
    {
        return;
    }

    eventPhase.Value = 1;
}
```

The host writes `EventPhase`. Clients observe the value and update local presentation from the accepted state.

## Example: Client Intent, Host Result

```csharp
public class PurchaseRequest
{
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class PurchaseAccepted
{
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int NewStock { get; set; }
}

public class PurchaseRequestMessage : TypedP2PMessage<PurchaseRequest>
{
    public override string MessageType => "MYMOD_PURCHASE_REQUEST";

    public PurchaseRequestMessage()
    {
    }

    public PurchaseRequestMessage(PurchaseRequest payload)
        : base(payload)
    {
    }
}

public class PurchaseAcceptedMessage : TypedP2PMessage<PurchaseAccepted>
{
    public override string MessageType => "MYMOD_PURCHASE_ACCEPTED";

    public PurchaseAcceptedMessage()
    {
    }

    public PurchaseAcceptedMessage(PurchaseAccepted payload)
        : base(payload)
    {
    }
}

client.RegisterMessageHandler<PurchaseRequestMessage>((message, senderId) =>
{
    PurchaseRequest request = message.Payload;
    if (!client.IsHost || !CanAcceptPurchase(senderId, request))
    {
        return;
    }

    var accepted = ApplyPurchase(request);
    _ = client.BroadcastMessageAsync(new PurchaseAcceptedMessage(accepted));
});

await client.SendMessageToPlayerAsync(hostId, new PurchaseRequestMessage(new PurchaseRequest
{
    ItemId = "og-kush",
    Quantity = 2
}));
```

The client sends `PurchaseRequestMessage` to the host. The host validates money, stock, distance, and game rules. Only then does the host broadcast `PurchaseAcceptedMessage` or update a `HostSyncVar<T>` containing the resulting stock count.

Do not let every client directly write the shared stock value. That creates conflicts and gives clients authority over state they do not own.

## Example: Per-Player State

```csharp
private ClientSyncVar<bool>? isReady;

private void CreateReadyState()
{
    var options = new NetworkSyncOptions { KeyPrefix = "MyMod_" };

    isReady = client.CreateClientSyncVar("Ready", false, options);
    isReady.OnValueChanged += (playerId, _, ready) =>
    {
        MelonLogger.Msg($"Player {playerId.m_SteamID} ready: {ready}");
    };
}

private void SetReady(bool ready)
{
    isReady!.Value = ready;
}
```

Each player owns their own `Ready` value. Everyone can read every player's value.

## Update Frequency

Steam lobby and member data are not a per-frame replication channel. For frequent values:

- Add `MaxSyncsPerSecond` to SyncVars.
- Batch related changes with `AutoSync = false` and `FlushPending()`.
- Send events when something meaningful changes instead of syncing continuously.
- Keep payloads small and use large-data helpers for files or large bundles.

## Multiplayer Checklist

Before calling a mod multiplayer compatible, verify:

- The mod still works when Steam networking is unavailable.
- The host can create a lobby and a client can join.
- Every synced key uses a unique mod prefix.
- Host-owned state rejects or ignores client writes.
- Client requests are validated by the host before state changes.
- SyncVars converge after join, leave, and rejoin.
- P2P handlers validate payloads before mutating state.
- Event handlers and SyncVars are cleaned up on lobby leave or mod shutdown.
- Mono and Il2Cpp builds reference the matching SteamNetworkLib release DLL.
