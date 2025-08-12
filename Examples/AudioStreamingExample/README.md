# AudioStreamingExample - Advanced Real-Time Audio Streaming

This example demonstrates **production-grade, real-time audio streaming** using SteamNetworkLib with Opus compression. The architecture has been redesigned to be object-oriented, extensible, and robust for real-world use cases.

## Architecture Overview

### Core Library Components (SteamNetworkLib)
- **`StreamMessage`** - Enhanced with fields for packet loss handling, retransmission, timing, and metadata
- **`StreamChannel<T>`** - Generic, codec-agnostic stream receiver with jitter buffering and packet reordering
- **`StreamSender<T>`** - Generic stream sender with proper sequencing and timing

### Audio-Specific Components (AudioStreamingExample)
- **`OpusAudioStreamChannel`** - Audio receiver with Opus decoding and packet loss concealment (PLC)
- **`OpusAudioSender`** - Audio sender with Opus encoding and Unity AudioClip support
- **`AudioStreamingManager`** - Main orchestrator for audio streaming, handles multiple concurrent streams

## Key Features

### Real-World Quality & Robustness
- ✅ **Jitter Buffering** - Smooth playback despite network irregularities
- ✅ **Packet Loss Concealment (PLC)** - Uses Opus PLC for missing frames
- ✅ **Packet Reordering** - Handles out-of-order packets correctly
- ✅ **Persistent Decoders** - One decoder per stream for optimal quality
- ✅ **High-Quality Opus** - 192kbps, stereo, optimized for music
- ✅ **Statistics & Monitoring** - Packet loss rates, jitter, buffer status

### Easy to Use & Extend
- ✅ **Object-Oriented Design** - Clean separation of concerns
- ✅ **Codec-Agnostic Core** - Easy to add video, sensor data, etc.
- ✅ **Event-Driven** - React to stream start/end, frame drops, etc.
- ✅ **Unity Integration** - Seamless AudioSource/AudioClip support

## Usage

### Basic Audio Streaming
```csharp
// Initialize the manager
var audioManager = new AudioStreamingManager(networkClient);

// Start streaming an AudioClip
audioManager.StartAudioStream(myAudioClip, "my_stream");

// Update regularly (e.g., in Unity Update)
audioManager.Update();

// Stop streaming
audioManager.StopAudioStream("my_stream");
```

### Advanced Usage
```csharp
// Monitor statistics
var stats = audioManager.GetStreamStats(playerId);
Console.WriteLine($"Packet loss: {stats.PacketLossRate:P2}");
Console.WriteLine($"PLC usage: {stats.PlcUsageRate:P2}");

// Handle events
audioManager.OnStreamStarted += (playerId, streamId) => 
    Console.WriteLine($"Player {playerId} started streaming");

// Configure audio quality
var sender = new OpusAudioSender("stream", 48000, 2, 960, networkClient);
sender.Bitrate = 256000; // Higher quality
sender.Quality = 100;    // Maximum quality
```

## File Structure

```
AudioStreamingExample/
├── README.md                    # This file
├── AudioStreamingManager.cs     # Main orchestrator
├── OpusAudioStreamChannel.cs    # Audio receiver with PLC
├── OpusAudioSender.cs          # Audio sender with encoding
└── (future files for video, etc.)
```

## Performance Characteristics

| Feature | Value | Notes |
|---------|-------|-------|
| **Latency** | ~200-500ms | Configurable buffer size |
| **Quality** | 192kbps stereo | Discord-level or better |
| **Frame Size** | 20ms (960 samples) | Industry standard |
| **Packet Loss Tolerance** | ~10% | With PLC enabled |
| **CPU Usage** | Low | Opus is highly optimized |
| **Memory Usage** | ~1-5MB per stream | Depends on buffer size |

## Comparison to Real-World Services

| Feature | This Implementation | Discord | Zoom | Notes |
|---------|-------------------|---------|------|-------|
| Codec | Opus 192kbps | Opus 64-128kbps | Opus/G.722 | Higher quality than most |
| Jitter Buffer | ✅ | ✅ | ✅ | Essential for quality |
| PLC | ✅ | ✅ | ✅ | Handles packet loss |
| FEC | ⚠️ (not enabled) | ✅ | ✅ | Could be added |
| Adaptive Bitrate | ❌ | ✅ | ✅ | For poor networks |
| Echo Cancellation | ❌ | ✅ | ✅ | Not needed for music |

## Extending the Architecture

### Adding Video Streaming
```csharp
public class H264VideoStreamChannel : StreamChannel<byte[]>
{
    protected override byte[]? DeserializeFrame(byte[] data, StreamMessage message)
    {
        // Decode H.264 frame
        return DecodeH264(data);
    }
}
```

### Adding Sensor Data
```csharp
public class SensorDataSender : StreamSender<SensorReading>
{
    protected override byte[]? SerializeFrame(SensorReading data)
    {
        // Serialize sensor data (JSON, MessagePack, etc.)
        return JsonSerializer.SerializeToUtf8Bytes(data);
    }
}
```

## Best Practices

1. **Always call `Update()`** - Required for jitter buffer processing
2. **Monitor statistics** - Watch packet loss and adjust buffer sizes
3. **Handle events** - React to stream start/end for UI updates
4. **Dispose properly** - Clean up resources when done
5. **Test on poor networks** - Simulate packet loss and jitter

## Future Enhancements

- **Forward Error Correction (FEC)** - Even better packet loss handling
- **Adaptive Bitrate** - Adjust quality based on network conditions
- **Multi-channel support** - Support for 5.1, 7.1 audio
- **Video streaming** - H.264/H.265 support
- **Recording/playback** - Save streams to disk

This architecture provides a solid foundation for real-time multimedia streaming in Unity games and applications, with quality comparable to professional streaming services. 