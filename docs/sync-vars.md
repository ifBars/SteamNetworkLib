# Synchronized Variables (SyncVars)

SteamNetworkLib provides a high-level API for synchronized variables that automatically keep values in sync across all lobby members with minimal boilerplate.

## Quick Start

```csharp
// Host-authoritative: only host can modify, all can read
var roundNumber = client.CreateHostSyncVar("Round", 1);

// Client-owned: each client owns their value, all can read everyone's
var isReady = client.CreateClientSyncVar("Ready", false);
```

## HostSyncVar - Host-Authoritative Data

Use `HostSyncVar<T>` when you need a single shared value that only the lobby host can modify:

```csharp
// Create
var gameSettings = client.CreateHostSyncVar("Settings", new GameSettings());
var maxScore = client.CreateHostSyncVar("MaxScore", 100);

// Subscribe to changes
gameSettings.OnValueChanged += (oldVal, newVal) =>
{
    MelonLogger.Msg($"Settings changed!");
};

// Modify (only works for host - silently ignored otherwise)
maxScore.Value = 200;

// Read (works for everyone)
int current = maxScore.Value;
```

### Key Points

- Only the lobby host can set the value
- Non-host writes are **silently ignored** (no exceptions)
- Enable `WarnOnIgnoredWrites` in options for debugging
- Uses Steam lobby data under the hood

## ClientSyncVar - Per-Client Data

Use `ClientSyncVar<T>` when each client needs their own synced value:

```csharp
// Create
var isReady = client.CreateClientSyncVar("Ready", false);
var playerLoadout = client.CreateClientSyncVar("Loadout", "default");

// Subscribe to any client's changes
isReady.OnValueChanged += (playerId, oldVal, newVal) =>
{
    MelonLogger.Msg($"Player {playerId} ready: {newVal}");
};

// Subscribe to only my changes
isReady.OnMyValueChanged += (oldVal, newVal) =>
{
    MelonLogger.Msg($"I am now ready: {newVal}");
};

// Set my own value
isReady.Value = true;

// Read my value
bool myReady = isReady.Value;

// Read another client's value
bool player2Ready = isReady.GetValue(player2Id);

// Get all clients' values
Dictionary<CSteamID, bool> allReady = isReady.GetAllValues();
bool everyoneReady = allReady.Values.All(r => r);
```

### Key Points

- Each client can only modify their own value
- All clients can read all other clients' values
- Missing values return the default value
- Uses Steam lobby member data under the hood

## Supported Types

The default JSON serializer supports:

| Category | Types |
|----------|-------|
| Primitives | `int`, `long`, `float`, `double`, `bool`, `string`, `byte`, `short`, `uint`, `ulong`, `decimal` |
| Enums | Any enum type (serialized as integer) |
| Collections | `List<T>`, `T[]`, `Dictionary<string, T>` |
| Custom Types | Classes/structs with parameterless constructor and public properties |

### Custom Type Example

```csharp
public class GameSettings
{
    public int MaxPlayers { get; set; } = 4;
    public string MapName { get; set; } = "default";
    public bool FriendlyFire { get; set; } = false;
    public List<string> EnabledMods { get; set; } = new();
}

// Usage
var settings = client.CreateHostSyncVar("Settings", new GameSettings());
```

**Requirements for custom types:**
1. Must have a public parameterless constructor
2. Properties must be public with both getter and setter
3. Property types must themselves be serializable
4. No circular references

## Configuration Options

Customize behavior with `NetworkSyncOptions`:

```csharp
var options = new NetworkSyncOptions
{
    // Log warnings when non-host tries to write (debugging)
    WarnOnIgnoredWrites = true,
    
    // Add prefix to avoid key collisions with other mods
    KeyPrefix = "MyMod_",
    
    // Disable auto-sync for manual batching (see below)
    AutoSync = false,
    
    // Rate limit syncs (e.g., 10 per second for position updates)
    MaxSyncsPerSecond = 10,
    
    // Throw exceptions on validation errors (default: false)
    ThrowOnValidationError = false,
    
    // Use custom serializer (optional)
    Serializer = new MyCustomSerializer()
};

var score = client.CreateHostSyncVar("Score", 0, options);
```

## Value Validation

Add validation constraints to ensure values meet requirements before syncing:

```csharp
// Range validation (built-in)
var scoreValidator = new RangeValidator<int>(0, 9999);
var score = client.CreateHostSyncVar("Score", 0, null, scoreValidator);

// Predicate validation (simple custom logic)
var teamNameValidator = new PredicateValidator<string>(
    value => value.Length >= 3 && value.Length <= 15,
    "Team name must be 3-15 characters"
);
var teamName = client.CreateClientSyncVar("Team", "Alpha", null, teamNameValidator);

// Composite validation (combine multiple rules)
var usernameValidator = new CompositeValidator<string>(
    new PredicateValidator<string>(
        v => v.Length >= 3 && v.Length <= 20,
        "Username must be 3-20 characters"
    ),
    new PredicateValidator<string>(
        v => char.IsLetter(v[0]),
        "Username must start with a letter"
    )
);
```

**Validation behavior:**
- Invalid values are rejected before syncing
- By default, validation errors are logged and trigger `OnSyncError`
- Set `ThrowOnValidationError = true` to throw exceptions instead
- Failed writes do not change the current value

## Rate Limiting

Limit how frequently a SyncVar can sync to prevent network spam:

```csharp
var positionOptions = new NetworkSyncOptions
{
    MaxSyncsPerSecond = 10  // Max 10 position updates per second
};

var positionX = client.CreateClientSyncVar("PosX", 0f, positionOptions);

// Rapid updates - automatically throttled to 10/sec
for (int i = 0; i < 100; i++)
{
    positionX.Value = i * 0.1f;  // Only ~10 of these will actually sync
}

// Check if there's a pending value waiting to sync
if (positionX.IsDirty)
{
    // Force immediate sync, bypassing rate limit
    positionX.FlushPending();
}
```

## Batch Syncing / Manual Sync

Disable `AutoSync` to make multiple changes before syncing:

```csharp
var batchOptions = new NetworkSyncOptions { AutoSync = false };

var gamePhase = client.CreateHostSyncVar("Phase", "Lobby", batchOptions);
var roundNumber = client.CreateHostSyncVar("Round", 0, batchOptions);

// Make multiple changes locally - nothing syncs yet
gamePhase.Value = "InGame";
roundNumber.Value = 1;

// Check which vars have unsaved changes
if (gamePhase.IsDirty || roundNumber.IsDirty)
{
    // Sync all changes at once
    gamePhase.FlushPending();
    roundNumber.FlushPending();
}
```

**Use cases for manual syncing:**
- Atomic multi-variable updates
- Reducing network traffic when changing multiple values
- Deferring sync until a specific game event

## Advanced Example

For a comprehensive example demonstrating validation, rate limiting, and batch syncing, see:

**[`Examples/AdvancedSyncVarExample.cs`](../Examples/AdvancedSyncVarExample.cs)**

This example includes:
- Range validation with error handling
- Rate-limited position updates
- Batch syncing for state transitions
- Custom validators with complex rules
- Interactive hotkeys to test each feature

## Custom Serialization

Implement `ISyncSerializer` for custom serialization:

```csharp
public class MySerializer : ISyncSerializer
{
    public string Serialize<T>(T value)
    {
        // Your serialization logic
    }
    
    public T Deserialize<T>(string data)
    {
        // Your deserialization logic
    }
    
    public bool CanSerialize(Type type)
    {
        // Return true if type is supported
    }
}

// Usage
var options = new NetworkSyncOptions { Serializer = new MySerializer() };
var data = client.CreateHostSyncVar("Data", myValue, options);
```

## Complete Example

For a comprehensive, production-ready example demonstrating all SyncVar features, see:

**[`Examples/SyncVarExample.cs`](../Examples/SyncVarExample.cs)**

This example includes:
- Host-authoritative game state (round tracking, settings, timer)
- Client-owned state (ready system, teams, loadouts)
- Custom serializable types (classes and enums)
- Event handling and error management
- Interactive test hotkeys (F1-F7)
- Ready-check system with real game logic

Run the example to see SyncVars in action with live synchronization across multiple clients.

## Lifecycle Management

SyncVars are **automatically cleaned up** when:
- You leave a lobby (`OnLobbyLeft`)
- The `SteamNetworkClient` is disposed

**No manual disposal required!** Just create them and forget about cleanup.

```csharp
// Create sync vars
var score = client.CreateHostSyncVar("Score", 0);
var ready = client.CreateClientSyncVar("Ready", false);

// Use them...
score.Value = 100;
ready.Value = true;

// When you leave the lobby or dispose the client,
// all sync vars are automatically disposed - no cleanup code needed!
```

## Error Handling

```csharp
var score = client.CreateHostSyncVar("Score", 0);

// Subscribe to sync errors
score.OnSyncError += (exception) =>
{
    MelonLogger.Error($"Sync error: {exception.Message}");
};

// For debugging non-host writes
score.OnWriteIgnored += (attemptedValue) =>
{
    MelonLogger.Warning($"Write ignored: {attemptedValue}");
};
```

## When to Use Which

| Use Case | SyncVar Type |
|----------|--------------|
| Game settings | `HostSyncVar` |
| Round/match state | `HostSyncVar` |
| Shared timer | `HostSyncVar` |
| Player ready status | `ClientSyncVar` |
| Player loadout/class | `ClientSyncVar` |
| Player preferences | `ClientSyncVar` |
| Per-player scores | `ClientSyncVar` |
