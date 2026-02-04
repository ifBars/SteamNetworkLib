#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Integration tests for SteamNetworkLib using isolated game instances
.DESCRIPTION
    This script creates two separate copies of Schedule 1 with different Goldberg configs
    to avoid Steam ID conflicts. This is more reliable than the shared config approach.
#>

param(
    [string]$GamePath = "D:\SteamLibrary\steamapps\common\Schedule I_alternate",
    [string]$HostSteamId = "76561199320154780",
    [string]$ClientSteamId = "76561199485712034",
    [int]$TestTimeoutSeconds = 45
)

$ErrorActionPreference = "Stop"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "SteamNetworkLib Real Game Integration Tests (Isolated)" -ForegroundColor Cyan
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

# Create isolated test directories
$testRoot = Join-Path $env:USERPROFILE "AppData\Local\Temp\SteamNetworkLib.RealGameTests"
$hostDir = Join-Path $testRoot "host"
$clientDir = Join-Path $testRoot "client"

if (Test-Path $testRoot) {
    Write-Host "Cleaning up previous test directories..." -ForegroundColor Gray
    Remove-Item -Path $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Creating isolated test directories..." -ForegroundColor Gray
New-Item -ItemType Directory -Path $hostDir -Force | Out-Null
New-Item -ItemType Directory -Path $clientDir -Force | Out-Null

# Copy essential files to isolated directories
function Copy-GameFiles {
    param(
        [string]$SourcePath,
        [string]$DestPath
    )
    
    # Copy exe
    Copy-Item -Path (Join-Path $SourcePath "Schedule I.exe") -Destination $DestPath -Force
    
    # Copy data directory
    $sourceData = Join-Path $SourcePath "Schedule I_Data"
    $destData = Join-Path $DestPath "Schedule I_Data"
    
    if (Test-Path $sourceData) {
        Copy-Item -Path $sourceData -Destination $destData -Recurse -Force
    }
    
    # Copy other essential files
    $essentialFiles = @("UnityCrashHandler64.exe", "UnityPlayer.dll", "steam_appid.txt")
    foreach ($file in $essentialFiles) {
        $sourcePath = Join-Path $SourcePath $file
        if (Test-Path $sourcePath) {
            Copy-Item -Path $sourcePath -Destination $DestPath -Force
        }
    }
}

Write-Host "[1/3] Copying game files for host..." -ForegroundColor Yellow
Copy-GameFiles -SourcePath $GamePath -DestPath $hostDir
Write-Host "[OK] Host files copied" -ForegroundColor Green

Write-Host "[2/3] Copying game files for client..." -ForegroundColor Yellow
Copy-GameFiles -SourcePath $GamePath -DestPath $clientDir
Write-Host "[OK] Client files copied" -ForegroundColor Green

# Create Goldberg configs
function Set-GoldbergConfigIsolated {
    param(
        [string]$GameDir,
        [string]$SteamId,
        [string]$Name
    )
    
    $configDir = Join-Path $GameDir "Schedule I_Data\Plugins\x86_64\steam_settings"
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

Write-Host "[3/3] Configuring Goldberg emulator..." -ForegroundColor Yellow
Set-GoldbergConfigIsolated -GameDir $hostDir -SteamId $HostSteamId -Name "TestHost"
Set-GoldbergConfigIsolated -GameDir $clientDir -SteamId $ClientSteamId -Name "TestClient"

Write-Host ""
Write-Host "Launching Host Instance..." -ForegroundColor Yellow
$hostExe = Join-Path $hostDir "Schedule I.exe"
$hostProcess = Start-Process -FilePath $hostExe -ArgumentList "--host" -WorkingDirectory $hostDir -PassThru -WindowStyle Normal
Write-Host "[OK] Host launched (PID: $($hostProcess.Id))" -ForegroundColor Green
Write-Host "Waiting 15 seconds for host to create lobby..." -ForegroundColor Gray
Start-Sleep -Seconds 15

Write-Host ""
Write-Host "Launching Client Instance..." -ForegroundColor Yellow
$clientExe = Join-Path $clientDir "Schedule I.exe"
$clientProcess = Start-Process -FilePath $clientExe -ArgumentList "--join" -WorkingDirectory $clientDir -PassThru -WindowStyle Normal
Write-Host "[OK] Client launched (PID: $($clientProcess.Id))" -ForegroundColor Green
Write-Host "Waiting $TestTimeoutSeconds seconds for client to join and test P2P..." -ForegroundColor Gray
Start-Sleep -Seconds $TestTimeoutSeconds

Write-Host ""
Write-Host "Cleanup..." -ForegroundColor Yellow

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

# Cleanup test directories
Write-Host "Cleaning up test directories..." -ForegroundColor Gray
Start-Sleep -Seconds 2  # Give processes time to fully exit
Remove-Item -Path $testRoot -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "[OK] Temporary files cleaned up" -ForegroundColor Green

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Test execution completed!" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Note: This was a manual verification test." -ForegroundColor Yellow
Write-Host "Check the game windows to verify lobby creation and P2P connectivity." -ForegroundColor Yellow
Write-Host ""
Write-Host "For automated testing, use the unit tests:" -ForegroundColor Yellow
Write-Host "  dotnet test -c Mono --filter 'FullyQualifiedName!~Integration'" -ForegroundColor Gray
