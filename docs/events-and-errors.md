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

```csharp
try
{
    var lobby = await client.CreateLobbyAsync();
}
catch (LobbyException ex)
{
    MelonLogger.Error($"Lobby error: {ex.Message}");
}
catch (SteamNetworkException ex)
{
    MelonLogger.Error($"Steam error: {ex.Message}");
}
```

## IL2CPP specifics

`client.ProcessIncomingMessages()` internally calls `SteamAPI.RunCallbacks()` on IL2CPP, which is required to drive Steam callbacks. Ensure you call it every frame (e.g., `OnUpdate`).

