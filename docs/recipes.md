# Recipes

Short, focused examples for common tasks.

## Host-authoritative sync var

```csharp
var roundNumber = client.CreateHostSyncVar("Round", 1);

// Subscribe to changes
roundNumber.OnValueChanged += (oldVal, newVal) =>
{
    MelonLogger.Msg($"Round {oldVal} -> {newVal}");
};

// Host sets new value (clients only read)
roundNumber.Value = 2;
```

## Per-client sync var

```csharp
var isReady = client.CreateClientSyncVar("Ready", false);

// Subscribe to any player's changes
isReady.OnValueChanged += (playerId, oldVal, newVal) =>
{
    MelonLogger.Msg($"Player {playerId}: ready={newVal}");
};

// Set my own ready status
isReady.Value = true;

// Check if everyone is ready
var allReady = isReady.GetAllValues().Values.All(r => r);
```

## Broadcast a mod configuration to everyone

```csharp
var cfg = new DataSyncMessage { Key = "mod_config", Value = JsonConvert.SerializeObject(config) };
await client.BroadcastMessageAsync(cfg);
```

## RPC-like event to a single player

```csharp
var evt = new EventMessage
{
    EventType = "give_item",
    Payload = "soil")
};
await client.SendMessageToPlayerAsync(targetId, evt);
```

## Send a screenshot file to the host

```csharp
var bytes = File.ReadAllBytes("screenshot.png");
int chunk = client.P2PManager.MaxPacketSize;
int total = (int)Math.Ceiling((double)bytes.Length / chunk);

for (int i = 0; i < total; i++)
{
    var slice = bytes.Skip(i * chunk).Take(chunk).ToArray();
    var msg = new FileTransferMessage
    {
        FileName = "screenshot.png",
        FileSize = bytes.Length,
        ChunkIndex = i,
        TotalChunks = total,
        IsFileData = true,
        ChunkData = slice
    };
    await client.SendMessageToPlayerAsync(hostId, msg, channel: 1);
}
```

## Invite friends via Steam overlay

```csharp
client.OpenInviteDialog();
```

## Check mod version compatibility

```csharp
client.SetMyData("mod_version", MyMod.Version);
client.SyncModDataWithAllPlayers("mod_version", MyMod.Version);
if (!client.IsModDataCompatible("mod_version"))
{
    MelonLogger.Warning("Players have mismatched mod versions");
}
```
