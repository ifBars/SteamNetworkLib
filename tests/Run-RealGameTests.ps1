#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Integration tests for SteamNetworkLib using the real Schedule 1 game installation
.DESCRIPTION
    This script launches two instances of Schedule 1 with different Steam IDs to test
    real P2P networking functionality using the Goldberg emulator.
    
    IMPORTANT: This script uses a shared Goldberg config file, so proper timing is critical:
    1. Host config is written and host launches
    2. Host fully initializes with its Steam ID (takes ~25 seconds)
    3. Client config overwrites the shared file
    4. Client launches with its own Steam ID
    
    If both instances have the same Steam ID, increase -HostInitDelaySeconds parameter.
#>

param(
    [string]$GamePath = "D:\SteamLibrary\steamapps\common\Schedule I_alternate",
    [string]$HostSteamId = "76561199320154780",
    [string]$ClientSteamId = "76561199485712034",
    [int]$TestTimeoutSeconds = 45,
    [int]$HostInitDelaySeconds = 25,
    [int]$ConfigWriteDelaySeconds = 5
)

$ErrorActionPreference = "Stop"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "SteamNetworkLib Real Game Integration Tests" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Game Path: $GamePath" -ForegroundColor Gray
Write-Host "Configuration: Mono" -ForegroundColor Gray
Write-Host ""

# Validate game installation
if (-not (Test-Path $GamePath)) {
    Write-Error "Game installation not found at: $GamePath"
    exit 1
}

$exePath = Join-Path $GamePath "Schedule I.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Schedule I.exe not found at: $exePath"
    exit 1
}

# Check for Goldberg emulator
$goldbergDll = Join-Path $GamePath "Schedule I_Data\Plugins\x86_64\steam_api64.dll"
if (-not (Test-Path $goldbergDll)) {
    Write-Warning "steam_api64.dll not found - Goldberg emulator may not be installed"
}

# Create Goldberg config function
function Set-GoldbergConfig {
    param(
        [string]$Mode,
        [string]$SteamId,
        [string]$Name
    )
    
    $configDir = Join-Path $GamePath "Schedule I_Data\Plugins\x86_64\steam_settings"
    $configFile = Join-Path $configDir "configs.user.ini"
    
    if (-not (Test-Path $configDir)) {
        New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    }
    
    $configContent = @"
[user::general]
account_name=$Name
account_steamid=$SteamId
language=english
"@
    
    Set-Content -Path $configFile -Value $configContent -Encoding UTF8
    Write-Host "[OK] Goldberg config set for $Name (SteamID: $SteamId)" -ForegroundColor Green
}

# Shared lobby file
$sharedLobbyDir = Join-Path $env:USERPROFILE "AppData\Local\Temp\SteamNetworkLib.RealGameTests"
$sharedLobbyFile = Join-Path $sharedLobbyDir "lobby.txt"

if (Test-Path $sharedLobbyDir) {
    Remove-Item -Path $sharedLobbyDir -Recurse -Force
}
New-Item -ItemType Directory -Path $sharedLobbyDir -Force | Out-Null

Write-Host "Step 1: Launching Host Instance..." -ForegroundColor Yellow
Set-GoldbergConfig -Mode "host" -SteamId $HostSteamId -Name "TestHost"
Write-Host "Waiting $ConfigWriteDelaySeconds seconds to ensure config is written..." -ForegroundColor Gray
Start-Sleep -Seconds $ConfigWriteDelaySeconds

# Launch host
$hostProcess = Start-Process -FilePath $exePath -ArgumentList "--host" -PassThru -WindowStyle Normal
Write-Host "[OK] Host launched (PID: $($hostProcess.Id))" -ForegroundColor Green
Write-Host "Waiting $HostInitDelaySeconds seconds for host to fully initialize with its Steam ID and create lobby..." -ForegroundColor Gray
Start-Sleep -Seconds $HostInitDelaySeconds

Write-Host ""
Write-Host "Step 2: Launching Client Instance..." -ForegroundColor Yellow
Write-Host "Updating Goldberg config for client..." -ForegroundColor Gray
Set-GoldbergConfig -Mode "client" -SteamId $ClientSteamId -Name "TestClient"
Write-Host "Waiting $ConfigWriteDelaySeconds seconds to ensure config is written..." -ForegroundColor Gray
Start-Sleep -Seconds $ConfigWriteDelaySeconds

# Launch client
$clientProcess = Start-Process -FilePath $exePath -ArgumentList "--join" -PassThru -WindowStyle Normal
Write-Host "[OK] Client launched (PID: $($clientProcess.Id))" -ForegroundColor Green
Write-Host "Waiting $TestTimeoutSeconds seconds for client to join and test P2P..." -ForegroundColor Gray
Start-Sleep -Seconds $TestTimeoutSeconds

Write-Host ""
Write-Host "Step 3: Cleanup..." -ForegroundColor Yellow

# Kill processes
try {
    if (-not $hostProcess.HasExited) {
        Stop-Process -Id $hostProcess.Id -Force
        Write-Host "[OK] Host process terminated" -ForegroundColor Green
    }
} catch {
    Write-Warning "Failed to stop host process: $_"
}

try {
    if (-not $clientProcess.HasExited) {
        Stop-Process -Id $clientProcess.Id -Force
        Write-Host "[OK] Client process terminated" -ForegroundColor Green
    }
} catch {
    Write-Warning "Failed to stop client process: $_"
}

# Cleanup shared files
if (Test-Path $sharedLobbyDir) {
    Remove-Item -Path $sharedLobbyDir -Recurse -Force
    Write-Host "[OK] Temporary files cleaned up" -ForegroundColor Green
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Test execution completed!" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Note: This was a manual verification test." -ForegroundColor Yellow
Write-Host "Check the game windows to verify lobby creation and P2P connectivity." -ForegroundColor Yellow
Write-Host ""
Write-Host "For automated testing, use the unit tests:" -ForegroundColor Yellow
Write-Host "  dotnet test SteamNetworkLib\tests\SteamNetworkLib.Tests -c Mono --filter 'FullyQualifiedName!~Integration'" -ForegroundColor Gray
