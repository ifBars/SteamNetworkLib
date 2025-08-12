# SteamNetworkLib

A streamlined, object-oriented Steam networking library designed specifically for **MelonLoader mods**. This library dramatically simplifies Steam lobby management, data synchronization, and P2P communication compared to using Steamworks.NET directly.

## 🎯 Why SteamNetworkLib?

### Before (Raw Steamworks.NET):
```csharp
// Complex Steam callback setup
private Callback<LobbyCreated_t> _lobbyCreatedCallback;
private Callback<LobbyEnter_t> _lobbyEnteredCallback;
private Callback<LobbyChatUpdate_t> _chatUpdateCallback;
private TaskCompletionSource<LobbyInfo> _createLobbyTcs;

// Manual callback handling
private void OnLobbyCreatedCallback(LobbyCreated_t result)
{
    if (result.m_eResult != EResult.k_EResultOK)
    {
        _createLobbyTcs?.SetException(new Exception($"Failed: {result.m_eResult}"));
        return;
    }
    // ... dozens of lines of callback management
}

// Manual P2P packet processing
while (SteamNetworking.IsP2PPacketAvailable(out uint packetSize))
{
    var data = new byte[packetSize];
    if (SteamNetworking.ReadP2PPacket(data, packetSize, out uint bytesRead, out CSteamID remoteId))
    {
        // ... manual packet deserialization and routing
    }
}
```

### After (SteamNetworkLib):
```csharp
// Simple, clean API
var steamNetwork = new SteamNetworkClient();
steamNetwork.Initialize();

// One-liner lobby creation
var lobby = await steamNetwork.CreateLobbyAsync();

// Easy data management
steamNetwork.SetLobbyData("game_mode", "cooperative");
steamNetwork.SetMyData("player_class", "warrior");

// Simple P2P messaging
steamNetwork.BroadcastTextMessage("Hello, lobby!");
steamNetwork.RegisterMessageHandler<DataSyncMessage>(OnDataReceived);

// Automatic packet processing
steamNetwork.ProcessIncomingMessages(); // Call in Update()
```

## 🚀 Key Features

### **🏢 Lobby Management**
- **Create/Join lobbies** with async/await support
- **Automatic member tracking** with join/leave events
- **Steam overlay integration** for invites

### **📊 Data Synchronization**
- **Lobby Data**: Global key-value storage for all players
- **Member Data**: Per-player data visible to everyone  
- **Automatic caching** and change detection
- **Event-driven updates** when data changes

### **🌐 P2P Communication**
- **Type-safe message system** with automatic serialization
- **Broadcast or direct messaging** between players
- **Built-in message types**: Text, DataSync, DataRequest, FileTransfer
- **Custom message support** via inheritance

### **🔧 MelonLoader Optimized**
- **No complex setup** - just initialize and go
- **Automatic resource cleanup** on dispose
- **Mod compatibility checking** (like BetterJukebox track manifests)
- **Error handling** that won't crash your mod

## 📦 Installation

1. Add `SteamNetworkLib.dll` to your MelonLoader mod references
2. Ensure your game uses Steamworks.NET
3. Initialize in your mod's `OnInitializeMelon()`

## 🔥 Real-World Example (MelonLoader Mod)

Here's how you'd replace complex Steam networking code:

```csharp
public class MyMod : MelonMod
{
    private SteamNetworkClient _steamNetwork;
    
    public override void OnInitializeMelon()
    {
        _steamNetwork = new SteamNetworkClient();
        if (_steamNetwork.Initialize())
        {
            // Set up event handlers
            _steamNetwork.OnMemberJoined += OnPlayerJoined;
            _steamNetwork.RegisterMessageHandler<DataSyncMessage>(OnDataSync);
            
            MelonLogger.Msg("✓ Steam networking ready!");
        }
    }

    public override void OnUpdate()
    {
        // Process incoming messages (replaces complex packet handling)
        _steamNetwork?.ProcessIncomingMessages();
    }

    private async void OnPlayerJoined(object sender, MemberJoinedEventArgs e)
    {
        MelonLogger.Msg($"Player joined: {e.Member.DisplayName}");
        
        // Send them our mod data (like BetterJukebox track manifest)
        await _steamNetwork.SendDataSyncAsync(e.Member.SteamId, "mod_version", "1.2.3");
        
        // Check if all players have compatible mods
        if (_steamNetwork.IsModDataCompatible("mod_version"))
        {
            MelonLogger.Msg("✓ All players compatible!");
        }
    }

    private void OnDataSync(DataSyncMessage message, CSteamID sender)
    {
        MelonLogger.Msg($"Received: {message.Key} = {message.Value}");
        
        // Handle mod data like track manifests, player stats, etc.
        if (message.Key == "track_manifest")
        {
            HandleTrackCompatibility(sender, message.Value);
        }
    }
}
```

## 🎵 Inspired by Real Usage

This library was created by analyzing the networking patterns in **BetterJukebox** - a complex MelonLoader mod that:
- ✅ Syncs custom track manifests between players
- ✅ Transfers files via P2P packets  
- ✅ Manages lobby data for compatibility
- ✅ Handles member data for per-player state

**SteamNetworkLib reduces 500+ lines of complex Steam networking code down to ~50 lines of clean, maintainable code.**

## 📚 Core Components

### **SteamNetworkClient**
Main entry point - combines all functionality:
```csharp
var client = new SteamNetworkClient();
client.Initialize();
```

### **Lobby Management**
```csharp
// Create/join lobbies
var lobby = await client.CreateLobbyAsync(ELobbyType.k_ELobbyTypeFriendsOnly, 8);
await client.JoinLobbyAsync(lobbyId);

// Manage members
var members = client.GetLobbyMembers();
client.InviteFriend(friendId);
```

### **Data Synchronization**
```csharp
// Lobby-wide data (global state)
client.SetLobbyData("game_mode", "cooperative");
var gameMode = client.GetLobbyData("game_mode");

// Per-player data (visible to all)
client.SetMyData("player_class", "warrior");
var allClasses = client.GetDataForAllPlayers("player_class");
```

### **P2P Communication**
```csharp
// Send messages
await client.SendTextMessageAsync(playerId, "Hello!");
client.BroadcastMessage(new DataSyncMessage { Key = "health", Value = "100" });

// Handle messages
client.RegisterMessageHandler<DataSyncMessage>(OnDataReceived);
```

## 🔧 Advanced Features

### **Mod Compatibility Checking**
```csharp
// Set your mod's compatibility data
client.SyncModDataWithAllPlayers("mod_hash", "abc123");

// Check if all players are compatible
if (client.IsModDataCompatible("mod_hash"))
{
    EnableMultiplayerFeatures();
}
```

### **High-Level Lobby Setup**
```csharp
// One method to set up a complete mod lobby
var lobby = await client.SetupModLobbyAsync("MyMod", "1.0.0", new Dictionary<string, string>
{
    ["difficulty"] = "hard",
    ["max_players"] = "4"
});
```

### **Custom Message Types**
```csharp
public class CustomMessage : P2PMessage
{
    public override string MessageType => "CUSTOM";
    public string CustomData { get; set; }
    
    public override byte[] Serialize() => /* your serialization */;
    public override void Deserialize(byte[] data) => /* your deserialization */;
}

client.RegisterMessageHandler<CustomMessage>(OnCustomMessage);
```

## 🛠️ Requirements

- **.NET Framework 4.7.2+** or **.NET 6.0+**
- **Steamworks.NET** (usually included with Unity/game)
- **Steam client** running
- **MelonLoader** (for mod development)

## 🔨 Building from Source

1. Clone this repository
2. Copy `Directory.Build.user.props.template` to `Directory.Build.user.props`
3. Edit `Directory.Build.user.props` and set your game installation path:
   ```xml
   <PropertyGroup>
     <GameInstallPath>C:\Path\To\Your\Game\Installation</GameInstallPath>
   </PropertyGroup>
   ```

Build configurations:
```powershell
# For Mono runtime
dotnet build -c Mono

# For Il2cpp runtime  
dotnet build -c Il2cpp
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### Third-Party Dependencies

SteamNetworkLib uses several third-party libraries, all of which are compatible with the MIT License:

- **Steamworks.NET** (MIT License) - Steam API wrapper
- **OpusSharp** (MIT License) - Audio compression for streaming examples (Mono runtime only)
- **MelonLoader** (MIT License) - Mod loader framework (examples only)
- **Newtonsoft.Json** (MIT License) - JSON serialization

> **Note**: OpusSharp is only included in Mono builds due to IL2CPP compatibility issues.

For complete license information, see [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md).

## 🤝 Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## 🔒 Security

If you discover a security vulnerability, please see [SECURITY.md](SECURITY.md) for reporting guidelines.

## Disclaimer

This library is provided "as-is" under the MIT License. The author is not responsible for any misuse, damages, or violations arising from the use of this wrapper. Users are responsible for complying with Valve’s Steamworks SDK license and any applicable laws.

---

**Built for the MelonLoader modding community** 🍉  
*Making Steam networking accessible to everyone.* 