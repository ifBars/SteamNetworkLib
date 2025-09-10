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

## Sending custom messages

Create a type by inheriting `P2PMessage` and implement `MessageType`, `Serialize`, `Deserialize`.

```csharp
using System.Text;

public class CustomMessage : P2PMessage
{
    public override string MessageType => "CUSTOM";
    public string Payload { get; set; } = string.Empty;

    public override byte[] Serialize()
    {
        var json = $"{{{CreateJsonBase(\"\\\"Payload\\\":\\\"{Payload}\\\"\")}}}";
        return Encoding.UTF8.GetBytes(json);
    }

    public override void Deserialize(byte[] data)
    {
        var json = Encoding.UTF8.GetString(data);
        ParseJsonBase(json);
        Payload = ExtractJsonValue(json, "Payload");
    }
}

client.RegisterMessageHandler<CustomMessage>((m, sender) => { /* ... */ });
await client.SendMessageToPlayerAsync(targetId, new CustomMessage { Payload = "Hi" });
```

## File transfer (chunked)

For files, send `FileTransferMessage` in chunks up to `client.P2PManager.MaxPacketSize`.

```csharp
var bytes = File.ReadAllBytes(path);
int chunkSize = client.P2PManager.MaxPacketSize; // use client wrappers for sending
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
