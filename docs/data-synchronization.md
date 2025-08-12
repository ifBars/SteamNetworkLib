# Data Synchronization

Use lobby-wide and per-player key-value data for lightweight shared state like versions, flags, and small strings.

## Lobby-wide data (host-only)

```csharp
// Set by the lobby owner
client.SetLobbyData("mod_version", "1.0.0");

// Read by anyone
string modVersion = client.GetLobbyData("mod_version");
```

## Per-player data

```csharp
// Local player sets their visible data
client.SetMyData("name", "bob");

// Read for self
string myClass = client.GetMyData("name");

// Read for any specific player
string otherClass = client.GetPlayerData(playerId, "name");

// Read the same key for everyone
Dictionary<CSteamID, string> allClasses = client.GetDataForAllPlayers("name");
```

## Change events

```csharp
client.OnLobbyDataChanged += (s, e) =>
{
    MelonLogger.Msg($"Lobby data: {e.Key} -> {e.NewValue}");
};

client.OnMemberDataChanged += (s, e) =>
{
    MelonLogger.Msg($"Member {e.MemberId}: {e.Key} -> {e.NewValue}");
};
```

## Version compatibility helper

Version checks are enabled by default. The client stores its library version under a reserved key and triggers `OnVersionMismatch` when players differ.

```csharp
client.OnVersionMismatch += (s, e) =>
{
    MelonLogger.Warning($"Version mismatch. Local: {e.LocalVersion}");
};

// Optional: toggle
client.VersionCheckEnabled = true;

// Manual check
bool ok = client.CheckLibraryVersionCompatibility();
```

## Batch updates

```csharp
client.SetMyDataBatch(new Dictionary<string, string>
{
    ["loadout"] = "1911",
    ["ready"] = "true",
});
```

## When to use P2P instead

- Use data keys for small strings/flags.
- For large payloads (files, images, audio), use the P2P Messaging API.
