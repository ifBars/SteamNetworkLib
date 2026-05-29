# Events and Error Handling

Understand the event model and exceptions to build resilient networking code.

## Client events

```csharp
client.OnLobbyCreated += (s, e) => { /* e.Lobby */ };
client.OnLobbyJoined  += (s, e) => { /* e.Lobby */ };
client.OnLobbyLeft    += (s, e) => { /* e.LobbyId, e.Reason */ };
client.OnMemberJoined += (s, e) => { /* e.Member */ };
client.OnMemberLeft   += (s, e) => { /* e.Member, e.Reason */ };

client.OnLobbyDataChanged  += (s, e) => { /* e.Key, e.OldValue, e.NewValue, e.ChangedBy */ };
client.OnMemberDataChanged += (s, e) => { /* e.MemberId, e.Key, e.OldValue, e.NewValue */ };

client.OnP2PMessageReceived += (s, e) =>
{
    // e.Message (P2PMessage), e.SenderId, e.Channel
};

client.OnVersionMismatch += (s, e) =>
{
    // e.LocalVersion, e.PlayerVersions, e.IncompatiblePlayers
};
```

Advanced P2P events exist on `SteamP2PManager` (packet-level and session events) if you need low-level control.

## Exceptions

- `SteamNetworkException`: Base exception for library errors.
- `LobbyException`: Lobby-specific failures (creation, join, invalid IDs).
- `P2PException`: P2P send/receive/session issues (target, channel, session error).
- `SyncException`: SyncVar and Steam data synchronization failures. `SyncSerializationException` and `SyncValidationException` also derive from this family.

All SteamNetworkLib exceptions expose structured diagnostics:

- `ErrorKind`: broad failure reason, such as `SteamUnavailable`, `NotInLobby`, `InvalidSteamId`, `PacketTooLarge`, `SerializationFailed`, `ValidationFailed`, or `MessageFormatInvalid`.
- `Operation`: API operation or lifecycle step that failed, when available.
- `IsRetryable`: whether retrying later may succeed.

Specialized exceptions include additional context. `LobbyException` can expose `LobbyId64`, `MemberId64`, `DataKey`, `SteamResult`, and `RequiresHost`. `P2PException` can expose `TargetId64`, `MessageType`, `PacketSize`, `MaxPacketSize`, `Channel`, and `SessionError`. `SyncException` exposes `SyncKey`.

```csharp
try
{
    var lobby = await client.CreateLobbyAsync();
}
catch (LobbyException ex)
{
    MelonLogger.Error($"Lobby error in {ex.Operation}: {ex.Message} ({ex.ErrorKind})");
    if (!string.IsNullOrEmpty(ex.DataKey))
    {
        MelonLogger.Error($"Data key: {ex.DataKey}");
    }
}
catch (SteamNetworkException ex)
{
    MelonLogger.Error($"Steam error in {ex.Operation}: {ex.Message} ({ex.ErrorKind})");
}
```

## Initialization failures

`Initialize()` throws a `SteamNetworkException` when Steamworks is not available. That usually means Steam is not running, the game was launched outside Steam, SteamAPI has not initialized yet, or another loader/runtime problem prevented Steamworks from attaching to the process.

For consumer mods, prefer `TryInitialize()` and retry later:

```csharp
if (!client.TryInitialize(out var error))
{
    MelonLogger.Warning($"Steam networking unavailable: {error?.Message} ({error?.ErrorKind})");
    multiplayerAvailable = false;
    return;
}

multiplayerAvailable = true;
```

Do not treat a failed initialization as a reason to disable the whole mod unless networking is the mod's only feature. Keep local behavior active, skip calls to SyncVars/P2P/lobby data, and retry after the menu or main scene has loaded.

## IL2CPP specifics

`client.ProcessIncomingMessages()` internally calls `SteamAPI.RunCallbacks()` on IL2CPP, which is required to drive Steam callbacks. Ensure you call it every frame (e.g., `OnUpdate`).

