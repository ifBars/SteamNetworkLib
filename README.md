# SteamNetworkLib

A streamlined, object-oriented Steam networking library designed specifically for **MelonLoader mods**. This library dramatically simplifies Steam lobby management, data synchronization, and P2P communication compared to using Steamworks.NET directly.

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

## 🛠️ Requirements

- **.NET Framework 4.7.2+** or **.NET 6.0+**
- **Steamworks.NET** (included with game)
- **Steam**
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

When consuming SteamNetworkLib from another mod project, download the runtime-matching DLL from the [GitHub releases page](https://github.com/ifBars/SteamNetworkLib/releases) and reference that local copy from your mod project:

- Mono config: reference the Mono release DLL
- Il2Cpp config: reference the Il2Cpp release DLL

Do not use the Mono DLL for Il2Cpp builds (or vice versa), and do not point your mod project at a sibling `..\SteamNetworkLib\bin\...` source checkout.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### Third-Party Dependencies

SteamNetworkLib uses several third-party libraries, all of which are compatible with the MIT License:

- **Steamworks.NET** (MIT License) - Steam API wrapper
- **OpusSharp** (MIT License) - Audio compression for streaming examples (Mono runtime only)
- **MelonLoader** (MIT License) - Mod loader framework (examples only)

> **Note**: OpusSharp is only included in Mono builds due to IL2CPP compatibility issues.

For complete license information, see [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md).

## 🤝 Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## 🔒 Security

If you discover a security vulnerability, please see [SECURITY.md](SECURITY.md) for reporting guidelines.

## Disclaimer

This library is provided "as-is" under the MIT License. The author is not responsible for any misuse, damages, or violations arising from the use of this wrapper. Users are responsible for complying with Valve’s Steamworks SDK license and any applicable laws.

---

**Built for the Schedule 1 modding community**
