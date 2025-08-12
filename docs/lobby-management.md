# Lobby Management

This page covers creating, joining, leaving, and inviting players to Steam lobbies using `SteamNetworkClient`.

## Quick start

```csharp
SteamNetworkClient client = new SteamNetworkClient();
client.Initialize();

// Create a friends-only lobby with up to 8 members
LobbyInfo lobby = await client.CreateLobbyAsync(ELobbyType.k_ELobbyTypeFriendsOnly, 8);

// Join an existing lobby
await client.JoinLobbyAsync(lobbyId);

// Leave the current lobby
client.LeaveLobby();

// Read current members
List<MemberInfo> members = client.GetLobbyMembers();

// Invite a friend or open Steam overlay invite
client.InviteFriend(friendId);
client.OpenInviteDialog();
```

## Events

Subscribe to lobby lifecycle and membership changes:

```csharp
client.OnLobbyCreated += (s, e) =>
{
    MelonLogger.Msg($"Lobby created: {e.Lobby.LobbyId}");
};

client.OnLobbyJoined += (s, e) =>
{
    MelonLogger.Msg($"Joined lobby: {e.Lobby.LobbyId}");
};

client.OnLobbyLeft += (s, e) =>
{
    MelonLogger.Msg($"Left lobby {e.LobbyId}: {e.Reason}");
};

client.OnMemberJoined += (s, e) =>
{
    MelonLogger.Msg($"Member joined: {e.Member.DisplayName}");
};

client.OnMemberLeft += (s, e) =>
{
    MelonLogger.Msg($"Member left: {e.Member.DisplayName}");
};
```

Event args reference:

- `LobbyCreatedEventArgs`
- `LobbyJoinedEventArgs`
- `LobbyLeftEventArgs`
- `MemberJoinedEventArgs`
- `MemberLeftEventArgs`

## Tips

- Only the lobby owner can change lobby-wide data; see Data Synchronization.
- Check `client.IsInLobby` and `client.IsHost` for state-aware UI and commands.
- Use Steam overlay invites for the smoothest UX: `client.OpenInviteDialog()`.


