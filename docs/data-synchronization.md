# Data Synchronization

> **Recommendation**: For most use cases, use **[Synchronized Variables (SyncVars)](sync-vars.md)** instead. SyncVars provide a cleaner API with automatic type safety, validation, rate limiting, and built-in prefix handling via `NetworkSyncOptions.KeyPrefix`.

Use lobby-wide and per-player key-value data for lightweight shared state like versions, flags, and small strings.

## Raw API vs SyncVars

**SyncVars are recommended** over the raw lobby/member data API for most use cases:

| Feature | Raw API | SyncVars |
|---------|---------|----------|
| Prefix handling | Manual | Automatic via `NetworkSyncOptions.KeyPrefix` |
| Type safety | String-only | Type-safe generic `T` |
| Validation | Manual | Built-in validators |
| Rate limiting | Manual | Built-in `MaxSyncsPerSecond` |
| Events | Raw change events | Typed `OnValueChanged` events |
| Batch updates | `SetMyDataBatch()` | Auto-sync batching |

**Use SyncVars** for game state, player data, and most mod data. See [Synchronized Variables (SyncVars)](sync-vars.md) for details.

---

## Important: Use Unique Prefixes

**Always use custom prefixes for your mod's data keys to avoid collisions with other mods.**

```csharp
// Good: Use a unique prefix for your mod
const string PREFIX = "MyMod_";

client.SetLobbyData($"{PREFIX}version", "1.0.0");
client.SetMyData($"{PREFIX}loadout", "1911");

// Bad: Generic keys may collide with other mods
client.SetLobbyData("version", "1.0.0");  // May conflict!
client.SetMyData("loadout", "1911");      // May conflict!
```

**Tip**: SyncVars handle prefixes automatically. Just set `KeyPrefix` in `NetworkSyncOptions`:

```csharp
var options = new NetworkSyncOptions { KeyPrefix = "MyMod_" };
var score = client.CreateHostSyncVar("Score", 0, options);
// Actual Steam key: "MyMod_Score" - no manual prefix needed!
```

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
