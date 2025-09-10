# Advanced API

For experienced users, SteamNetworkLib exposes its core components for direct control. Use these when you need fine‑grained behavior beyond the high‑level `SteamNetworkClient` helpers.

## Components
- `SteamLobbyManager` — Create/join/leave lobbies, invites, member tracking.
- `SteamLobbyData` — Lobby‑wide key/value store.
- `SteamMemberData` — Per‑player key/value store.
- `SteamP2PManager` — P2P packets/messages, sessions, channels, reliability.
- `NetworkRules` — Runtime configuration for relay, channel polling, session policy.

Access via:
```csharp
var client = new SteamNetworkClient(rules);
client.Initialize();
var lobby = client.LobbyManager;
var p2p   = client.P2PManager;
```

## P2P Manager
- Sending
```csharp
// Typed message
await p2p.SendMessageAsync(targetId, new TextMessage { Content = "Hi" }, channel: 0, sendType: EP2PSend.k_EP2PSendReliable);

// Raw packet
p2p.BroadcastPacket(bytes, channel: 1, sendType: EP2PSend.k_EP2PSendUnreliable);
```
- Receiving
```csharp
// Call regularly (or use client.ProcessIncomingMessages())
p2p.ProcessIncomingPackets();
```
- Handlers
```csharp
p2p.RegisterMessageHandler<TextMessage>((msg, sender) => { /* ... */ });
```
- Sessions
```csharp
var active = p2p.GetActiveSessions();
var state  = p2p.GetSessionState(someId);
p2p.CloseSession(someId);
```
- Limits
```csharp
int max = p2p.MaxPacketSize; // keep chunks <= max
```

## Lobby Manager
```csharp
// Create / Join
var lobbyInfo = await lobby.CreateLobbyAsync(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers: 4);
// await lobby.JoinLobbyAsync(lobbyId);

// Members & invites
var members = lobby.GetLobbyMembers();
lobby.InviteFriend(friendId);

// Leave
lobby.LeaveLobby();
```

## Data APIs
```csharp
// Lobby‑wide
var map = client.LobbyData.GetData("map");
client.LobbyData.SetData("map", "arena");

// Per‑player
client.MemberData.SetMemberData("class", "mage");
string? cls = client.MemberData.GetMemberData(playerId, "class");
```

## Rules (Advanced)
```csharp
// Update at runtime
var rules = client.NetworkRules;
rules.AcceptOnlyFriends = true;    // gate P2P session requests
rules.MinReceiveChannel = 0;       // IL2CPP polling range
rules.MaxReceiveChannel = 3;
client.UpdateNetworkRules(rules);
```
Notes
- Setting `EnableRelay` applies `SteamNetworking.AllowP2PPacketRelay(...)`.
- In IL2CPP, polling respects the channel range from `NetworkRules`.
- High‑level handlers still fire (`client.OnP2PMessageReceived`) when you use `p2p` directly.

