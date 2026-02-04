#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fully automated P2P integration tests using a custom MelonMod
.DESCRIPTION
    Builds the test mod, deploys it to Schedule 1, runs automated tests,
    and reports results. Tests actual P2P messaging between host and client.
#>

param(
    [string]$GamePath = "D:\SteamLibrary\steamapps\common\Schedule I_alternate",
    [string]$HostSteamId = "76561199320154780",
    [string]$ClientSteamId = "76561199485712034",
    [int]$HostInitDelaySeconds = 25,
    [int]$ConfigWriteDelaySeconds = 5,
    [int]$TestTimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "SteamNetworkLib Automated Integration Tests" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
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

$modsPath = Join-Path $GamePath "Mods"
if (-not (Test-Path $modsPath)) {
    Write-Error "Mods directory not found. Is MelonLoader installed?"
    exit 1
}

# Shared results directory
$sharedDir = Join-Path $env:LOCALAPPDATA "Temp\SteamNetworkLib.TestMod"
$resultsFile = Join-Path $sharedDir "results.txt"
$lobbyFile = Join-Path $sharedDir "lobby.txt"

if (Test-Path $sharedDir) {
    Remove-Item -Path $sharedDir -Recurse -Force
}
New-Item -ItemType Directory -Path $sharedDir -Force | Out-Null

Write-Host "Step 1: Building Test Mod..." -ForegroundColor Yellow
$testModProject = Join-Path $PSScriptRoot "SteamNetworkLib.TestMod\SteamNetworkLib.TestMod.csproj"

if (-not (Test-Path $testModProject)) {
    Write-Error "Test mod project not found at: $testModProject"
    exit 1
}

try {
    $buildOutput = dotnet build $testModProject -c Mono 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed:`n$buildOutput"
        exit 1
    }
    Write-Host "[OK] Test mod built successfully" -ForegroundColor Green
}
catch {
    Write-Error "Build error: $_"
    exit 1
}

# Copy mod DLLs to game
Write-Host "Step 2: Deploying Test Mod..." -ForegroundColor Yellow
$testModDll = Join-Path $PSScriptRoot "SteamNetworkLib.TestMod\bin\Mono\net6.0\SteamNetworkLib.TestMod.dll"
$steamNetLibDll = Join-Path $PSScriptRoot "SteamNetworkLib.TestMod\bin\Mono\net6.0\SteamNetworkLib.dll"

if (-not (Test-Path $testModDll)) {
    Write-Error "Test mod DLL not found at: $testModDll"
    exit 1
}

Copy-Item -Path $testModDll -Destination $modsPath -Force
Copy-Item -Path $steamNetLibDll -Destination $modsPath -Force
Write-Host "[OK] Mod deployed to $modsPath" -ForegroundColor Green

# Goldberg config function
function Set-GoldbergConfig {
    param(
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
}

Write-Host ""
Write-Host "Step 3: Launching Host Instance..." -ForegroundColor Yellow
Set-GoldbergConfig -SteamId $HostSteamId -Name "TestHost"
Start-Sleep -Seconds $ConfigWriteDelaySeconds

$hostProcess = Start-Process -FilePath $exePath -ArgumentList "--host" -PassThru -WindowStyle Minimized
Write-Host "[OK] Host launched (PID: $($hostProcess.Id))" -ForegroundColor Green
Write-Host "Waiting $HostInitDelaySeconds seconds for host initialization..." -ForegroundColor Gray
Start-Sleep -Seconds $HostInitDelaySeconds

Write-Host ""
Write-Host "Step 4: Launching Client Instance..." -ForegroundColor Yellow
Set-GoldbergConfig -SteamId $ClientSteamId -Name "TestClient"
Start-Sleep -Seconds $ConfigWriteDelaySeconds

$clientProcess = Start-Process -FilePath $exePath -ArgumentList "--join" -PassThru -WindowStyle Minimized
Write-Host "[OK] Client launched (PID: $($clientProcess.Id))" -ForegroundColor Green
Write-Host "Waiting $TestTimeoutSeconds seconds for tests to complete..." -ForegroundColor Gray

# Monitor for test completion
$elapsed = 0
$interval = 2
$hostResults = $null
$clientResults = $null

while ($elapsed -lt $TestTimeoutSeconds) {
    Start-Sleep -Seconds $interval
    $elapsed += $interval
    
    # Check if both processes exited
    if ($hostProcess.HasExited -and $clientProcess.HasExited) {
        Write-Host "Both processes have exited" -ForegroundColor Gray
        break
    }
    
    # Check for results file
    if (Test-Path $resultsFile) {
        $results = Get-Content $resultsFile -Raw
        Write-Host "Results detected: $results" -ForegroundColor Gray
        break
    }
    
    Write-Host "." -NoNewline -ForegroundColor Gray
}

Write-Host ""
Write-Host ""
Write-Host "Step 5: Analyzing Results..." -ForegroundColor Yellow

# Kill processes if still running
if (-not $hostProcess.HasExited) {
    Stop-Process -Id $hostProcess.Id -Force
    Write-Host "Terminated host process" -ForegroundColor Gray
}

if (-not $clientProcess.HasExited) {
    Stop-Process -Id $clientProcess.Id -Force
    Write-Host "Terminated client process" -ForegroundColor Gray
}

# Read results
$testsPassed = $false

if (Test-Path $resultsFile) {
    $results = Get-Content $resultsFile -Raw
    Write-Host "Results found: $results" -ForegroundColor Gray
    
    if ($results -match "PASS") {
        $testsPassed = $true
    }
}

# Read logs from MelonLoader
$latestLogPath = Join-Path $GamePath "MelonLoader\Latest.log"
if (Test-Path $latestLogPath) {
    $logContent = Get-Content $latestLogPath -Raw
    
    # Extract test results from log
    $testResults = $logContent | Select-String -Pattern "\[(PASS|FAIL)\]" -AllMatches
    
    if ($testResults.Matches.Count -gt 0) {
        Write-Host ""
        Write-Host "Test Results from Log:" -ForegroundColor Cyan
        foreach ($match in ($logContent -split "`n" | Where-Object { $_ -match "\[(PASS|FAIL)\]" })) {
            if ($match -match "PASS") {
                Write-Host $match -ForegroundColor Green
            } else {
                Write-Host $match -ForegroundColor Red
            }
        }
    }
    
    # Check for "ALL TESTS PASSED"
    if ($logContent -match "ALL.*TESTS PASSED") {
        $testsPassed = $true
    }
}

Write-Host ""
Write-Host "Step 6: Cleanup..." -ForegroundColor Yellow

# Remove test mod from game
Remove-Item -Path (Join-Path $modsPath "SteamNetworkLib.TestMod.dll") -Force -ErrorAction SilentlyContinue
Write-Host "[OK] Test mod removed from game" -ForegroundColor Green

# Cleanup shared directory
if (Test-Path $sharedDir) {
    Remove-Item -Path $sharedDir -Recurse -Force
    Write-Host "[OK] Temporary files cleaned up" -ForegroundColor Green
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan

if ($testsPassed) {
    Write-Host "ALL TESTS PASSED!" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "TESTS FAILED!" -ForegroundColor Red
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Check MelonLoader logs for details:" -ForegroundColor Yellow
    Write-Host "  $latestLogPath" -ForegroundColor Gray
    exit 1
}
