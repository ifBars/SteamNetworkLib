# Introduction

## What is SteamNetworkLib?

SteamNetworkLib is a powerful C# wrapper library built on top of **Steamworks.NET** that dramatically simplifies Steam networking functionality for Unity games and applications. It provides a clean, intuitive API for common networking tasks that would otherwise require extensive boilerplate code and deep knowledge of the Steamworks API.

## Why SteamNetworkLib?

Working directly with Steamworks.NET can be challenging and error-prone. Especially for Schedule 1 mods, where you need to manage both Mono and IL2CPP branches of Steamworks.NET. SteamNetworkLib addresses these pain points by providing:

### 🚀 **Simplified API**
- **One-line operations** for complex networking tasks
- **Async/await support** for modern C# development
- **Intuitive method names** that clearly express intent
- **Comprehensive error handling** with meaningful exceptions

### 🎯 **Focused Functionality**
- **Lobby Management** — Create, join, and manage Steam lobbies effortlessly
- **Data Synchronization** — Simple key-value data sharing between players
- **P2P Communication** — Reliable peer-to-peer messaging system
- **Member Management** — Track players and their data automatically

### 🔧 **Developer-Friendly**
- **Extensive documentation** with practical examples
- **Full XML documentation** for IntelliSense support
- **MelonLoader optimized** for modding scenarios

## Key Features (at a glance)

- Lobby: `CreateLobbyAsync`, `JoinLobbyAsync`, `GetLobbyMembers()`
- Data: `SetLobbyData`, `SetMyData`, `GetPlayerData`
- P2P: `RegisterMessageHandler<T>`, `SendMessageToPlayerAsync`, `BroadcastMessageAsync`
- Dedicated servers: the same high-level API can run through DedicatedServerMod when connected to a dedicated server

See the dedicated guides for details:

- [Lobby Management](lobby-management.md)
- [Data Synchronization](data-synchronization.md)
- [P2P Messaging](p2p-messaging.md)
- [Dedicated Server Compatibility](dedicated-server-compatibility.md)
- [Events and Error Handling](events-and-errors.md)
- [Recipes](recipes.md)

## Architecture Overview

SteamNetworkLib is designed with modularity and ease of use in mind:

```
SteamNetworkClient (Main Entry Point)
├── SteamLobbyManager (Lobby operations)
├── SteamLobbyData (Lobby-wide data)
├── SteamMemberData (Player-specific data)
└── SteamP2PManager (Peer-to-peer communication)
```

- **[`SteamNetworkClient`](xref:SteamNetworkLib.SteamNetworkClient)** — Your main interface to all functionality
- **[`SteamLobbyManager`](xref:SteamNetworkLib.Core.SteamLobbyManager)** — Handles lobby creation, joining, and member management
- **[`SteamLobbyData`](xref:SteamNetworkLib.Core.SteamLobbyData)** — Manages lobby-wide key-value data storage
- **[`SteamMemberData`](xref:SteamNetworkLib.Core.SteamMemberData)** — Manages per-player key-value data storage
- **[`SteamP2PManager`](xref:SteamNetworkLib.Core.SteamP2PManager)** — Handles reliable message passing between players

## Getting Started

Ready to dive in? Head over to the [Getting Started](getting-started.md) guide to learn how to integrate SteamNetworkLib into your project in just a few minutes!
