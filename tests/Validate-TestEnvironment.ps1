#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick validation script for P2P test environment

.DESCRIPTION
    Validates that all required components are in place for automated P2P testing.
    Run this before attempting to run tests to ensure your environment is configured.

.EXAMPLE
    .\Validate-TestEnvironment.ps1
    Validates the test environment and reports any issues
#>

param(
    [switch]$Fix
)

$ErrorActionPreference = "Continue"

Write-Host "SteamNetworkLib P2P Test Environment Validator" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

$ScriptDir = $PSScriptRoot
$ProjectDir = Split-Path -Parent $ScriptDir
$SolutionDir = Split-Path -Parent $ProjectDir

$Issues = @()
$Warnings = @()

# 1. Check Directory.Build.user.props
Write-Host "[1/6] Checking Directory.Build.user.props..." -NoNewline
$UserProps = Join-Path $SolutionDir "Directory.Build.user.props"
$TemplateProps = Join-Path $SolutionDir "Directory.Build.user.props.template"

if (-not (Test-Path $UserProps)) {
    Write-Host " MISSING" -ForegroundColor Red
    $Issues += "Directory.Build.user.props not found"
    
    if ($Fix -and (Test-Path $TemplateProps)) {
        Copy-Item $TemplateProps $UserProps
        Write-Host "      → Created from template. Please edit and set your game path." -ForegroundColor Yellow
    }
} else {
    $content = Get-Content $UserProps -Raw
    if ($content -match '<MonoGameInstallPath>(.*?)</MonoGameInstallPath>') {
        $path = $Matches[1].Trim()
        if ($path -match 'Path' -or $path -eq '') {
            Write-Host " NOT CONFIGURED" -ForegroundColor Red
            $Issues += "MonoGameInstallPath is not set to a valid path"
        } elseif (-not (Test-Path $path)) {
            Write-Host " INVALID PATH" -ForegroundColor Red
            $Issues += "Game path does not exist: $path"
        } else {
            Write-Host " OK ($path)" -ForegroundColor Green
        }
    } else {
        Write-Host " MISSING PATH" -ForegroundColor Red
        $Issues += "MonoGameInstallPath property not found"
    }
}

# 2. Check Steamworks.NET
Write-Host "[2/6] Checking Steamworks.NET..." -NoNewline
if (Test-Path $UserProps) {
    $content = Get-Content $UserProps -Raw
    if ($content -match '<MonoGameInstallPath>(.*?)</MonoGameInstallPath>') {
        $gamePath = $Matches[1].Trim()
        $steamworksPath = Join-Path $gamePath "Schedule I_Data" "Managed" "com.rlabrecque.steamworks.net.dll"
        
        if (Test-Path $steamworksPath) {
            Write-Host " OK" -ForegroundColor Green
        } else {
            Write-Host " NOT FOUND" -ForegroundColor Red
            $Issues += "Steamworks.NET DLL not found at: $steamworksPath"
        }
    } else {
        Write-Host " SKIP (no game path)" -ForegroundColor Yellow
    }
} else {
    Write-Host " SKIP (no config)" -ForegroundColor Yellow
}

# 3. Check steam_api64.dll (Goldberg)
Write-Host "[3/6] Checking steam_api64.dll (Goldberg)..." -NoNewline
if (Test-Path $UserProps) {
    $content = Get-Content $UserProps -Raw
    if ($content -match '<MonoGameInstallPath>(.*?)</MonoGameInstallPath>') {
        $gamePath = $Matches[1].Trim()
        $steamApiPaths = @(
            (Join-Path $gamePath "steam_api64.dll"),
            (Join-Path $gamePath "Schedule I_Data" "Plugins" "x86_64" "steam_api64.dll")
        )
        
        $found = $false
        foreach ($path in $steamApiPaths) {
            if (Test-Path $path) {
                $found = $true
                Write-Host " OK ($path)" -ForegroundColor Green
                break
            }
        }
        
        if (-not $found) {
            Write-Host " NOT FOUND" -ForegroundColor Yellow
            $Warnings += "steam_api64.dll not found. Install Goldberg Steam Emulator from: https://github.com/Detanup01/gbe_fork/releases"
        }
    } else {
        Write-Host " SKIP (no game path)" -ForegroundColor Yellow
    }
} else {
    Write-Host " SKIP (no config)" -ForegroundColor Yellow
}

# 4. Check .NET SDK
Write-Host "[4/6] Checking .NET SDK..." -NoNewline
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -eq 0) {
    $majorVersion = [int]($dotnetVersion -split '\.' | Select-Object -First 1)
    if ($majorVersion -ge 6) {
        Write-Host " OK (v$dotnetVersion)" -ForegroundColor Green
    } else {
        Write-Host " OLD (v$dotnetVersion, need 6.0+)" -ForegroundColor Yellow
        $Warnings += ".NET SDK version $dotnetVersion found, but 6.0+ is recommended"
    }
} else {
    Write-Host " NOT FOUND" -ForegroundColor Red
    $Issues += ".NET SDK not found. Install from: https://dotnet.microsoft.com/download"
}

# 5. Check project files
Write-Host "[5/6] Checking project files..." -NoNewline
$testProject = Join-Path $ScriptDir "SteamNetworkLib.Tests" "SteamNetworkLib.Tests.csproj"
$workerProject = Join-Path $ScriptDir "SteamNetworkLib.P2PWorker" "SteamNetworkLib.P2PWorker.csproj"

if ((Test-Path $testProject) -and (Test-Path $workerProject)) {
    Write-Host " OK" -ForegroundColor Green
} else {
    Write-Host " MISSING" -ForegroundColor Red
    if (-not (Test-Path $testProject)) { $Issues += "Test project not found: $testProject" }
    if (-not (Test-Path $workerProject)) { $Issues += "Worker project not found: $workerProject" }
}

# 6. Check test utilities
Write-Host "[6/6] Checking test utilities..." -NoNewline
$goldbergHelper = Join-Path $ScriptDir "SteamNetworkLib.Tests" "TestUtilities" "GoldbergTestHelper.cs"
$testClientManager = Join-Path $ScriptDir "SteamNetworkLib.Tests" "TestUtilities" "TestClientManager.cs"

if ((Test-Path $goldbergHelper) -and (Test-Path $testClientManager)) {
    Write-Host " OK" -ForegroundColor Green
} else {
    Write-Host " MISSING" -ForegroundColor Red
    if (-not (Test-Path $goldbergHelper)) { $Issues += "GoldbergTestHelper.cs not found" }
    if (-not (Test-Path $testClientManager)) { $Issues += "TestClientManager.cs not found" }
}

# Summary
Write-Host ""
Write-Host "=================================================" -ForegroundColor Cyan

if ($Issues.Count -eq 0 -and $Warnings.Count -eq 0) {
    Write-Host "✓ Environment is ready for automated P2P testing!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Run tests with:" -ForegroundColor Cyan
    Write-Host "  .\Run-P2PTests.ps1" -ForegroundColor White
    exit 0
} else {
    if ($Issues.Count -gt 0) {
        Write-Host "✗ Issues found ($($Issues.Count)):" -ForegroundColor Red
        foreach ($issue in $Issues) {
            Write-Host "  • $issue" -ForegroundColor Red
        }
    }
    
    if ($Warnings.Count -gt 0) {
        Write-Host ""
        Write-Host "⚠ Warnings ($($Warnings.Count)):" -ForegroundColor Yellow
        foreach ($warning in $Warnings) {
            Write-Host "  • $warning" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host "Fix these issues before running tests." -ForegroundColor Cyan
    exit 1
}
