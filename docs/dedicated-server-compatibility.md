# Dedicated Server Compatibility

SteamNetworkLib supports two Schedule 1 networking modes through the same consumer API:

- `LobbyP2P`: the vanilla game path, using Steam lobbies plus Steam P2P packets.
- `DedicatedRelay`: a dedicated-server path, using DedicatedServerMod custom messaging. The server may route that messaging over FishNet RPC or Steam Networking Sockets.

Consumer mods should keep using `SteamNetworkClient`, `ProcessIncomingMessages()`, lobby/member data methods, and typed P2P messages. SteamNetworkLib detects the active mode at runtime.

## Runtime Behavior

When the player is in a vanilla Steam lobby, SteamNetworkLib keeps using the existing Steam lobby and Steam P2P APIs.

When the player is connected to a DedicatedServerMod server, SteamNetworkLib reflects the DedicatedServerMod `CustomMessaging` API and registers a compatibility session. DedicatedServerMod then sends a virtual lobby snapshot containing:

- local player SteamID
- server SteamID when available
- current member list
- lobby data
- per-member data

SteamNetworkLib exposes that snapshot through the normal high-level API:

```csharp
client.IsInLobby;
client.IsHost;
client.LocalPlayerId;
client.CurrentLobby;
client.GetLobbyMembers();
client.SetMyData("MyMod_Version", "1.0.0");
await client.BroadcastMessageAsync(new TextMessage { Content = "Hello" });
```

## P2P Semantics

In `LobbyP2P`, messages are sent through `SteamNetworking.SendP2PPacket`.

In `DedicatedRelay`, the same serialized SteamNetworkLib packet is sent to DedicatedServerMod, which forwards it to the target client or broadcasts it to session members. Logical P2P channel values are preserved so packet and message events report the same channel selected by the sender.

Reliability is still selected with the normal SNL APIs, but the dedicated path depends on the active DedicatedServerMod messaging backend for physical delivery. Operators can choose `FishNetRpc` or `SteamNetworkingSockets` in DedicatedServerMod configuration.

## Consumer Guidance

Most mods do not need dedicated-server-specific code. Keep these practices:

- Call `client.Initialize()` once after Steam is available.
- Call `client.ProcessIncomingMessages()` every update tick.
- Use `client.IsInLobby` instead of checking vanilla lobby internals.
- Use `client.IsHost` for host-authoritative behavior. In dedicated sessions, the virtual owner is selected by the server compatibility layer.
- Use `GetLobbyMembers()` and `LocalPlayerId` instead of caching vanilla Steam lobby state directly.

Do not replace Steam P2P code with dedicated-only logic. The library chooses the active route so the same mod can work in vanilla lobbies and dedicated-server sessions.
