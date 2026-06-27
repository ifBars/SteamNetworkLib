# Recipes

Short, focused examples for common tasks.

## Optional Steam initialization with retry

```csharp
private SteamNetworkClient client = new SteamNetworkClient();
private bool multiplayerAvailable;
private float nextRetryAt;

public override void OnUpdate()
{
    if (!multiplayerAvailable && Time.realtimeSinceStartup >= nextRetryAt)
    {
        if (client.TryInitialize(out var error))
        {
            multiplayerAvailable = true;
            MelonLogger.Msg("Steam networking ready.");
        }
        else
        {
            nextRetryAt = Time.realtimeSinceStartup + 2f;
            MelonLogger.Warning($"Steam networking not ready: {error?.Message}");
        }
    }

    if (multiplayerAvailable)
    {
        client.ProcessIncomingMessages();
    }
}
```

Guard every networking path with `multiplayerAvailable`. Your local/single-player logic should still run when Steamworks is unavailable.

## Find host and remote members

```csharp
if (client.TryGetHostMember(out var host))
{
    MelonLogger.Msg($"Host is {host.DisplayName} ({host.SteamId64})");
}

foreach (var member in client.GetRemoteMembers())
{
    MelonLogger.Msg($"Remote member {member.DisplayName}: {member.SteamIdString}");
}
```

## Send a typed transaction message

```csharp
public class TransactionPayload
{
    public string TransactionId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class TransactionMessage : TypedP2PMessage<TransactionPayload>
{
    public override string MessageType => "MYMOD_TRANSACTION";

    public TransactionMessage()
    {
    }

    public TransactionMessage(TransactionPayload payload)
        : base(payload)
    {
    }
}

client.RegisterMessageHandler<TransactionMessage>((message, sender) =>
{
    MelonLogger.Msg($"Received {message.Payload.TransactionId} from {sender.m_SteamID}");
});

await client.SendMessageToPlayerAsync(hostId, new TransactionMessage(new TransactionPayload
{
    TransactionId = Guid.NewGuid().ToString("N"),
    ItemId = "pseudo",
    Quantity = 10,
    UnitPrice = 25m
}));
```

## Host-approved client request

Use this shape when the client wants to do something that affects shared state, but the host must validate it first.

```csharp
public class CheckoutRequest : TypedP2PMessage<CheckoutRequestPayload>
{
    public override string MessageType => "MYMOD_CHECKOUT_REQUEST";

    public CheckoutRequest()
    {
    }

    public CheckoutRequest(CheckoutRequestPayload payload)
        : base(payload)
    {
    }
}

public class CheckoutRequestPayload
{
    public string RequestId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

client.RegisterMessageHandler<CheckoutRequest>((message, sender) =>
{
    var request = message.Payload;
    int approved = Math.Min(request.Quantity, GetAvailableStock(request.ItemId));
    ApplyHostApprovedCheckout(sender, request.ItemId, approved);
});
```

The client sends intent only. The host owns final state and publishes the result through a host snapshot, SyncVar, or explicit response message.

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

## Host-owned state snapshot

```csharp
public class LabelSnapshot
{
    public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
    public long Revision { get; set; }
}

var options = new NetworkSyncOptions { KeyPrefix = "MyLabels_" };
var labels = client.CreateHostSyncVar("Labels", new LabelSnapshot(), options);

labels.OnValueChanged += (oldValue, newValue) =>
{
    ApplyLabels(newValue.Labels);
};

if (client.IsHost)
{
    labels.Value = new LabelSnapshot
    {
        Labels = BuildCurrentLabels(),
        Revision = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };
}
```

Use snapshots for late joiners and resyncs. Use smaller P2P messages or SyncVars for frequent edits.

## Poll member data when callbacks are unreliable

```csharp
private ClientSyncVar<string> actionRequests;
private readonly HashSet<string> handledRequests = new HashSet<string>();
private float nextPollAt;

public override void OnUpdate()
{
    client.ProcessIncomingMessages();

    if (!client.IsHost || Time.realtimeSinceStartup < nextPollAt)
    {
        return;
    }

    nextPollAt = Time.realtimeSinceStartup + 2f;
    actionRequests.Refresh();

    foreach (var pair in actionRequests.GetAllValues())
    {
        if (string.IsNullOrWhiteSpace(pair.Value) || !handledRequests.Add(pair.Value))
        {
            continue;
        }

        HandleClientActionRequest(pair.Key, pair.Value);
    }
}
```

Keep this poll slow. It is a reliability fallback for important state, not a per-frame sync loop.

## RPC-like event to a single player

```csharp
var evt = new EventMessage
{
    EventType = "give_item",
    Payload = "soil"
};
await client.SendMessageToPlayerAsync(targetId, evt);
```

## Send a screenshot file to the host

```csharp
var bytes = File.ReadAllBytes("screenshot.png");
await client.SendLargeDataToPlayerAsync(hostId, "screenshot.png", bytes, channel: 1);
```

For manual chunking, keep each complete serialized packet under the reliable send limit. The payload chunk is smaller than the packet because SteamNetworkLib adds message headers.

```csharp
var bytes = File.ReadAllBytes("screenshot.png");
int chunk = 64 * 1024;
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
