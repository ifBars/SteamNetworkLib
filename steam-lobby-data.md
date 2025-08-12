This guide shows how to use Steamworks.NET to sync simple string data between players in multiplayer mods. Perfect for settings, configurations, or small mod states.

## Setup Callbacks
```csharp
// Initialize these callbacks in your mod's startup
private Callback<LobbyEnter_t> _lobbyEnteredCallback;
private Callback<LobbyChatUpdate_t> _chatUpdateCallback;

public void Initialize() {
#if MONO
    _lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    _chatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnPlayerEnterOrLeave);
#else
    // IL2CPP requires System.Action wrapper
    _lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(new Action<LobbyEnter_t>(OnLobbyEntered));
    _chatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(new Action<LobbyChatUpdate_t>(OnPlayerEnterOrLeave));
#endif
}
```

## Syncing Global Lobby Data (Host Only)
```csharp
// HOST sets data for EVERYONE to read
if (Singleton<Lobby>.Instance.IsHost) {
    // Store mod settings for all players to see
    SteamMatchmaking.SetLobbyData(
        Singleton<Lobby>.Instance.LobbySteamID,
        "my_mod_setting",
        "some_value"
    );
}

// ANY PLAYER can read global lobby data
string setting = SteamMatchmaking.GetLobbyData(
    Singleton<Lobby>.Instance.LobbySteamID,
    "my_mod_setting"
);
```

## Syncing Per-Player Data
```csharp
// Set YOUR OWN player-specific data
SteamMatchmaking.SetLobbyMemberData(
    Singleton<Lobby>.Instance.LobbySteamID,
    "player_state",
    "ready"
);

// Read OTHER PLAYERS' data
foreach (var playerId in Singleton<Lobby>.Instance.Players) {
    string playerState = SteamMatchmaking.GetLobbyMemberData(
        Singleton<Lobby>.Instance.LobbySteamID,
        playerId,
        "player_state"
    );
    
    if (playerState == "ready") {
        // This player is ready!
    }
}
```

## Handling Lobby Events
```csharp
private void OnLobbyEntered(LobbyEnter_t result) {
    // Called when you join a lobby
    MelonLogger.Msg($"Entered lobby: {result.m_ulSteamIDLobby}");
    
    // Set your player data immediately
    SteamMatchmaking.SetLobbyMemberData(
        new CSteamID(result.m_ulSteamIDLobby),
        "mod_version",
        "1.2.3"
    );
}

private void OnPlayerEnterOrLeave(LobbyChatUpdate_t result) {
    // Called when lobby membership changes
    MelonLogger.Msg($"Player {result.m_ulSteamIDUserChanged} " +
                   $"event: {result.m_rgfChatMemberStateChange}");
}
```

* **Data Limits:** ~8KB per player for member data, limited total size for lobby data 
* **Non-Host Safety:** Only hosts can set lobby data, but anyone can set their own member data
* **Persistence:** Lobby data persists as long as the lobby exists
* **Frequency:** Don't spam updates - change detection is better than constant updates
* **Storage:** For larger data, consider using P2P packet transfers

**Steamworks API Docs:** https://partner.steamgames.com/doc/api