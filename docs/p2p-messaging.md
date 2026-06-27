# P2P Messaging

Use the P2P layer to send reliable, typed messages and raw packets between players. Ideal for chat, gameplay events, file chunks, and streaming.

## Basics

```csharp
// Register handlers via the high-level client API
client.RegisterMessageHandler<TextMessage>((msg, sender) =>
{
    MelonLogger.Msg($"Message from {sender}: {msg.Content}");
});

// Send to one player
await client.SendMessageToPlayerAsync(targetId, new TextMessage { Content = "Hello!" });

// Broadcast to everyone
await client.BroadcastMessageAsync(new TextMessage { Content = "Welcome!" });

// Pump incoming packets every frame (e.g., in Update)
client.ProcessIncomingMessages();
```

## Sending typed payload messages

For most custom mod messages, prefer `TypedP2PMessage<TPayload>`. It keeps SteamNetworkLib metadata (`SenderId`, `Timestamp`) outside your payload and uses the same IL2CPP-compatible JSON serializer as SyncVars. Your payload should be a simple DTO with a public parameterless constructor and public get/set properties.

This is the recommended shape for transaction/state messages like AutoRestock restock operations, vehicle sync requests, label changes, or host-authored config deltas.

Use this API when you want:

- A stable `MessageType` routed through `RegisterMessageHandler<T>()`.
- A payload model that can evolve independently from SteamNetworkLib's message metadata.
- Nested DTOs for small gameplay state such as slot identifiers, item IDs, quantities, prices, and request IDs.
- Runtime-neutral payloads that work in both Mono and IL2CPP builds.

Use manual `P2PMessage` serialization only when you need binary data, compression, encryption, or an external serializer.

### Step 1: Define a payload DTO

```csharp
using SteamNetworkLib.Models;

public class RestockTransactionPayload
{
    public string TransactionId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public SlotIdentifier Slot { get; set; } = new SlotIdentifier();
}

public class SlotIdentifier
{
    public string Property { get; set; } = string.Empty;
    public string Grid { get; set; } = string.Empty;
    public int SlotIndex { get; set; }
    public float[] GridLocation { get; set; } = Array.Empty<float>();
}
```

### Step 2: Define the message type

```csharp
public class RestockTransactionMessage : TypedP2PMessage<RestockTransactionPayload>
{
    public override string MessageType => "MYMOD_RESTOCK_TRANSACTION";

    public RestockTransactionMessage()
    {
    }

    public RestockTransactionMessage(RestockTransactionPayload payload)
        : base(payload)
    {
    }
}
```

### Step 3: Register and send it

```csharp
client.RegisterMessageHandler<RestockTransactionMessage>((message, sender) =>
{
    RestockTransactionPayload payload = message.Payload;
    MelonLogger.Msg($"Restock request {payload.TransactionId} from {sender.m_SteamID}");
    ProcessRestock(payload);
});

await client.SendMessageToPlayerAsync(hostId, new RestockTransactionMessage(
    new RestockTransactionPayload
    {
        TransactionId = Guid.NewGuid().ToString("N"),
        ItemId = "pseudo",
        Quantity = 20,
        UnitPrice = 42.5m,
        Slot = currentSlotIdentifier
    }));
```

Do not put `CSteamID`, Unity objects, game objects, item instances, or slot references directly in the payload. Send stable IDs, primitive values, arrays/lists/dictionaries, and small nested DTOs, then resolve game objects locally when the message is handled.

### Payload contract guidelines

Typed payloads are mod-to-mod contracts. Treat them like save data:

| Use | Avoid |
| --- | --- |
| `string`, `int`, `float`, `double`, `decimal`, `bool`, `DateTime`, `Guid` | `CSteamID` inside payload DTOs |
| `ulong` Steam IDs such as `MemberInfo.SteamId64` | Unity `GameObject`, `Transform`, `MonoBehaviour`, or `ScriptableObject` instances |
| Arrays, lists, dictionaries, and small nested DTOs | Live inventory, item, storage, grid, or slot object references |
| Stable IDs and coordinates that can be resolved locally | Process-local object handles or scene-only references |

Prefer version-tolerant payloads for messages that may be sent between different mod versions. Add optional properties instead of renaming existing ones, and keep handlers defensive when a field may be empty or default.

### Handler registration

Register typed messages once after `TryInitialize()` succeeds:

```csharp
if (client.TryInitialize())
{
    client.RegisterMessageHandler<RestockTransactionMessage>(OnRestockTransaction);
}
```

`RegisterMessageHandler<T>()` also registers the custom message type with SteamNetworkLib's serializer. If a peer sends a message type that has not been registered locally, the raw packet cannot be routed to your handler.

## Correlated request/response messages

Use `P2PRequestResponseClient<TRequest, TResponse>` when a player needs one answer from a specific peer. The common case is a client asking the host to approve an action, such as checkout, permission checks, lock acquisition, or a host-authored state change.

The helper:

- Assigns a `RequestId` when the request does not already have one.
- Registers the response message handler.
- Tracks pending responses by `RequestId`.
- Times out requests that never receive a matching response.
- Provides a responder wrapper that copies the request ID onto the response.

### Define request and response messages

```csharp
public class CheckoutRequestPayload
{
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class CheckoutResponsePayload
{
    public string ReservationId { get; set; } = string.Empty;
    public int ApprovedQuantity { get; set; }
}

public class CheckoutRequestMessage : P2PRequestMessage<CheckoutRequestPayload>
{
    public override string MessageType => "MYMOD_CHECKOUT_REQUEST";

    public CheckoutRequestMessage()
    {
    }

    public CheckoutRequestMessage(CheckoutRequestPayload payload)
        : base(payload)
    {
    }
}

public class CheckoutResponseMessage : P2PResponseMessage<CheckoutResponsePayload>
{
    public override string MessageType => "MYMOD_CHECKOUT_RESPONSE";

    public CheckoutResponseMessage()
    {
    }

    public CheckoutResponseMessage(CheckoutResponsePayload payload)
        : base(payload)
    {
    }
}
```

### Host: register a responder

```csharp
private P2PRequestResponseClient<CheckoutRequestMessage, CheckoutResponseMessage>? checkoutRpc;

private void RegisterNetworking()
{
    checkoutRpc = client.CreateRequestResponseClient<CheckoutRequestMessage, CheckoutResponseMessage>(
        TimeSpan.FromSeconds(10));

    checkoutRpc.RegisterResponder((request, sender) =>
    {
        int approved = Math.Min(request.Body.Quantity, GetAvailableStock(request.Body.ItemId));

        return new CheckoutResponseMessage(new CheckoutResponsePayload
        {
            ReservationId = Guid.NewGuid().ToString("N"),
            ApprovedQuantity = approved
        })
        {
            Success = approved > 0,
            Error = approved > 0 ? string.Empty : "Out of stock"
        };
    });
}
```

### Client: send and await the response

```csharp
var checkoutRpc = client.CreateRequestResponseClient<CheckoutRequestMessage, CheckoutResponseMessage>(
    TimeSpan.FromSeconds(10));

var response = await checkoutRpc.SendRequestAsync(hostId, new CheckoutRequestMessage(
    new CheckoutRequestPayload
    {
        ItemId = "pseudo",
        Quantity = 12
    }));

if (response.Success)
{
    ApplyApprovedCheckout(response.Body.ReservationId, response.Body.ApprovedQuantity);
}
else
{
    MelonLogger.Warning($"Checkout denied: {response.Error}");
}
```

Keep request handlers host-authoritative when they mutate shared state. Clients should send intent (`ItemId`, quantity, stable slot IDs), and the host should validate the current game state before replying.

## Sending custom messages manually

Create a type by inheriting `P2PMessage` and implement `MessageType`, `Serialize`, `Deserialize`.

Use this lower-level path only when you need a custom binary format, compression, encryption, or a serializer not covered by `TypedP2PMessage<TPayload>`.

### Step 1: Define your custom message class

```csharp
using System.Text;
using SteamNetworkLib.Models;

public class TransactionMessage : P2PMessage
{
    public override string MessageType => "TRANSACTION";

    public string TransactionId { get; set; } = string.Empty;
    public string FromPlayer { get; set; } = string.Empty;
    public string ToPlayer { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";

    public override byte[] Serialize()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        return Encoding.UTF8.GetBytes(json);
    }

    public override void Deserialize(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TransactionMessage>(json);
        if (deserialized != null)
        {
            TransactionId = deserialized.TransactionId;
            FromPlayer = deserialized.FromPlayer;
            ToPlayer = deserialized.ToPlayer;
            Amount = deserialized.Amount;
            Currency = deserialized.Currency;
            SenderId = deserialized.SenderId;
            Timestamp = deserialized.Timestamp;
        }
    }
}
```

### Step 2: Register a handler for your custom message type

```csharp
public override void OnInitializeMelon()
{
    client = new SteamNetworkClient();
    if (client.TryInitialize())
    {
        // Register handler - this automatically registers the custom type
        client.RegisterMessageHandler<TransactionMessage>(OnTransactionReceived);
    }
    else
    {
        // Keep local behavior active and retry initialization later.
    }
}

private void OnTransactionReceived(TransactionMessage message, CSteamID sender)
{
    MelonLogger.Msg($"Transaction {message.TransactionId}: {message.Amount} {message.Currency}");
}
```

### Step 3: Send and receive custom messages

```csharp
// Send a custom message
var transaction = new TransactionMessage
{
    TransactionId = "txn-12345",
    FromPlayer = "Player1",
    ToPlayer = "Player2",
    Amount = 100.00m,
    Currency = "USD"
};

await client.SendMessageToPlayerAsync(targetId, transaction);

// Or broadcast to all players
client.BroadcastMessage(transaction);
```

### How it works

The library receives message type identifiers as strings and needs a mapping to C# classes for deserialization. When you call `RegisterMessageHandler<T>()`, the library automatically registers your custom type. Built-in types (TEXT, DATA_SYNC, FILE_TRANSFER, STREAM, HEARTBEAT, EVENT) are pre-registered.

## File transfer and large data

Steam P2P packet limits depend on the send type:

- `EP2PSend.k_EP2PSendUnreliable` and `EP2PSend.k_EP2PSendUnreliableNoDelay`: keep the total serialized packet at or below 1200 bytes.
- `EP2PSend.k_EP2PSendReliable` and `EP2PSend.k_EP2PSendReliableWithBuffering`: Steam supports reliable messages up to 1 MB and handles fragmentation/reassembly internally.

For large data that must arrive intact, prefer reliable chunks. The simplest path is to let SteamNetworkLib calculate safe `FileTransferMessage` chunk sizes:

```csharp
var bytes = File.ReadAllBytes(path);
await client.SendLargeDataToPlayerAsync(targetId, Path.GetFileName(path), bytes, channel: 1);
```

If you need manual control, send `FileTransferMessage` chunks sized for the full serialized packet, not just `ChunkData`. `client.P2PManager.MaxPacketSize` reports Steam's reliable 1 MB limit; headers still count against that limit.

```csharp
var bytes = File.ReadAllBytes(path);
int chunkSize = 64 * 1024; // payload bytes; keep serialized packet under the reliable limit
int total = (int)Math.Ceiling((double)bytes.Length / chunkSize);

for (int i = 0; i < total; i++)
{
    var slice = bytes.Skip(i * chunkSize).Take(chunkSize).ToArray();

    var file = new FileTransferMessage
    {
        FileName = Path.GetFileName(path),
        FileSize = bytes.Length,
        ChunkIndex = i,
        TotalChunks = total,
        IsFileData = true,
        ChunkData = slice
    };

    await client.SendMessageToPlayerAsync(targetId, file);
}
```

## Channels and reliability

- Default channel is 0; you can use multiple channels (e.g., 0 control, 1 files, 2 audio).
- Use `EP2PSend.k_EP2PSendReliable` for reliability; for streams, prefer the message-recommended send type.
- In dedicated-server sessions, SteamNetworkLib preserves the logical channel when routing through DedicatedServerMod. Physical reliability follows the active DedicatedServerMod messaging backend.

### Selecting channels and reliability automatically
Configure a policy once via `NetworkRules.MessagePolicy` and apply it at runtime:

```csharp
// Streams on channel 1 using the message's recommended send type;
// everything else reliable on channel 0
client.NetworkRules.MessagePolicy = msg =>
{
    if (msg is StreamMessage s) return (1, s.RecommendedSendType);
    return (0, client.NetworkRules.DefaultSendType);
};

client.UpdateNetworkRules(client.NetworkRules);
```

## Events and sessions

- `client.OnP2PMessageReceived` fires for any deserialized message.
- P2P sessions are managed automatically by the client.
