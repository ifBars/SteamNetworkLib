# SteamNetworkLib Test Setup Guide

Quick guide to get the sync var tests running with Goldberg Steam Emulator.

## Quick Start (5 Minutes)

### Step 1: Install Goldberg Steam Emulator

1. Download Goldberg from: https://github.com/Detanup01/gbe_fork/releases
2. Extract the `steam_api64.dll` from the download
3. This DLL will be automatically copied from your game installation (configured in Directory.Build.user.props)

### Step 2: Verify Configuration

Check that your `Directory.Build.user.props` file has correct paths:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <MonoGameInstallPath>D:\Path\To\Your\Game</MonoGameInstallPath>
    <MonoAssembliesPath>$(MonoGameInstallPath)\Schedule I_Data\Managed</MonoAssembliesPath>
  </PropertyGroup>
</Project>
```

**Important**: The test project expects `com.rlabrecque.steamworks.net.dll` in the Managed folder. This is the standard Steamworks.NET package name.

### Step 3: Place Goldberg DLL

Copy the Goldberg `steam_api64.dll` to your game installation directory (replacing the original):
```
D:\Path\To\Your\Game\steam_api64.dll
```

**OR** manually copy it to the test output directory after building:
```
tests\SteamNetworkLib.Tests\bin\Debug\netstandard2.1\steam_api64.dll
```

### Step 4: Build and Run

```bash
# From repository root
dotnet build tests/SteamNetworkLib.Tests/SteamNetworkLib.Tests.csproj
dotnet test tests/SteamNetworkLib.Tests/SteamNetworkLib.Tests.csproj
```

## Test Categories

### Unit Tests (No Steam Required)
Fast tests that don't need Goldberg or Steam API:
```bash
dotnet test --filter "FullyQualifiedName~Unit"
```

**Tests:**
- Serialization tests for JSON converter
- Validation logic tests
- No network, no Steam, pure C# logic

### Integration Tests (Requires Goldberg)
Full end-to-end tests simulating multiple Steam clients:
```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

**HostSyncVar Tests:**
- ✅ Host sets value → Client receives update
- ✅ Client tries to write → Write is ignored  
- ✅ Complex objects synchronize correctly
- ✅ Multiple clients all receive updates

**ClientSyncVar Tests:**
- ✅ Client sets value → Others receive update
- ✅ Each client maintains independent values
- ✅ GetAllValues returns all player data
- ✅ Complex objects sync per-player

## What Gets Tested?

### HostSyncVar<T> (Sync/HostSyncVar.cs:46)
- **Authority**: Only host can write
- **Storage**: Steam lobby data
- **Use Case**: Game state, round numbers, global settings

**Verified Behaviors:**
1. Host writes propagate to all clients
2. Client writes are silently ignored (with optional warning)
3. OnValueChanged events fire correctly
4. Complex object serialization works
5. Multiple clients receive updates simultaneously

### ClientSyncVar<T> (Sync/ClientSyncVar.cs:53)
- **Authority**: Each client owns their value
- **Storage**: Steam member data (per-player)
- **Use Case**: Ready status, player loadouts, scores

**Verified Behaviors:**
1. Each client can set their own value
2. All clients see each other's values
3. GetValue(playerId) retrieves specific player's value
4. GetAllValues() returns dictionary of all players
5. Complex objects synchronize per-player
6. OnValueChanged fires with correct Steam ID

## How It Works

The tests use **Goldberg Steam Emulator** to simulate multiple Steam clients in a single process:

```
Single Test Process
│
├─ TestClientManager("Host")
│  ├─ Goldberg Config (Steam ID: 76561197960265728)
│  ├─ Creates Lobby via Steam API
│  └─ SteamNetworkClient with HostSyncVar
│
├─ TestClientManager("Client1")
│  ├─ Goldberg Config (Steam ID: 76561197960265729)
│  ├─ Joins Lobby
│  └─ SteamNetworkClient with HostSyncVar
│
└─ TestClientManager("Client2")
   ├─ Goldberg Config (Steam ID: 76561197960265730)
   ├─ Joins Lobby
   └─ SteamNetworkClient with HostSyncVar
```

**Magic Sauce**: Goldberg's `user_steam_id.txt` allows each `TestClientManager` to have a unique Steam ID, simulating separate players on a local network.

## Expected Output

### Successful Test Run
```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 45 s
```

### Test Execution Flow (Example)
```
=== Test: Host sets value, client receives update ===
[TestHost] Initialized successfully (Steam ID: 76561197960265728)
[TestClient1] Initialized successfully (Steam ID: 76561197960265729)
[TestHost] Created lobby: 109775241870811136
[TestClient1] Joined lobby: 109775241870811136
Host setting value to 42...
Client received value change: 0 -> 42
✓ Test passed: Client received host's value update
```

## Troubleshooting

### ❌ "Could not find Steamworks.NET"
**Solution**: Update `MonoAssembliesPath` in Directory.Build.user.props to point to your game's Managed folder

### ❌ "Steam API initialization failed"
**Solution**: Ensure Goldberg's steam_api64.dll is in the test output directory

### ❌ Tests timeout
**Solution**: 
- Increase timeout values in test code
- Check Windows Firewall isn't blocking local network
- Ensure Goldberg config has `enable_lan_only_mode=1`

### ❌ "Lobby join failed"
**Solution**: Add longer delays after lobby creation (try 1000ms instead of 500ms)

### ❌ Sync vars don't synchronize
**Solution**: 
- Ensure both clients joined lobby successfully
- Increase wait time after setting values
- Check that Steam callbacks are processing (TestClientManager handles this automatically)

## Files Created

```
tests/
└── SteamNetworkLib.Tests/
    ├── SteamNetworkLib.Tests.csproj          # xUnit test project
    ├── README.md                              # Detailed test documentation
    │
    ├── TestUtilities/
    │   ├── GoldbergTestHelper.cs             # Goldberg configuration helper
    │   └── TestClientManager.cs               # Manages test Steam clients
    │
    ├── Unit/
    │   └── SyncSerializationTests.cs         # JSON serialization tests
    │
    └── Integration/
        ├── HostSyncVarIntegrationTests.cs    # Host sync var e2e tests
        └── ClientSyncVarIntegrationTests.cs  # Client sync var e2e tests
```

## Next Steps

1. **Run unit tests first** - They're fast and verify basic functionality
2. **Run a single integration test** - Verify Goldberg is working
3. **Run all integration tests** - Full validation
4. **Add your own tests** - Follow the patterns in existing tests

## Resources

- **Test README**: `tests/SteamNetworkLib.Tests/README.md` - Comprehensive documentation
- **Goldberg Emulator**: https://github.com/Detanup01/gbe_fork
- **SteamNetworkLib Docs**: `docs/sync-vars.md` - Sync var usage guide
- **xUnit Documentation**: https://xunit.net/

## Quick Reference

```bash
# Run all tests
dotnet test

# Run only fast unit tests
dotnet test --filter "FullyQualifiedName~Unit"

# Run only integration tests  
dotnet test --filter "FullyQualifiedName~Integration"

# Run specific test
dotnet test --filter "HostSyncVar_HostSetsValue_ClientReceivesUpdate"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# Build without running tests
dotnet build tests/SteamNetworkLib.Tests/SteamNetworkLib.Tests.csproj
```

---

**Note**: The errors you see in the IDE about missing types (CSteamID, Steamworks, etc.) are expected before the first build. Once you build the project with the correct paths configured, these will resolve as the Steamworks.NET.dll reference is loaded.
