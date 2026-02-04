#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automated P2P Host/Client Test Runner for SteamNetworkLib

.DESCRIPTION
    This script automates the execution of P2P integration tests using Goldberg Steam Emulator.
    It handles building, configuration validation, and test execution in a CI-friendly manner.

.PARAMETER TestFilter
    Filter tests by name pattern (e.g., "TextMessage", "HostSyncVar")

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Debug)

.PARAMETER SkipBuild
    Skip the build step and run tests with existing binaries

.PARAMETER VerboseOutput
    Enable verbose test output

.PARAMETER Timeout
    Test timeout in seconds (default: 120)

.PARAMETER ValidateOnly
    Only validate configuration without running tests

.EXAMPLE
    .\Run-P2PTests.ps1
    Runs all P2P tests with default settings

.EXAMPLE
    .\Run-P2PTests.ps1 -TestFilter "TextMessage" -VerboseOutput
    Runs only TextMessage tests with verbose output

.EXAMPLE
    .\Run-P2PTests.ps1 -ValidateOnly
    Validates configuration and exits
#>

param(
    [string]$TestFilter = "",
    [string]$Configuration = "Mono",
    [switch]$SkipBuild,
    [switch]$VerboseOutput,
    [int]$Timeout = 120,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"
$script:ExitCode = 0

# Colors for output
$ColorSuccess = "Green"
$ColorError = "Red"
$ColorWarning = "Yellow"
$ColorInfo = "Cyan"

function Write-Status($Message, $Color = $ColorInfo) {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" -ForegroundColor $Color
}

function Write-Success($Message) {
    Write-Status "[OK] $Message" $ColorSuccess
}

function Write-Error($Message) {
    Write-Status "[ERROR] $Message" $ColorError
}

function Write-Warning($Message) {
    Write-Status "[WARN] $Message" $ColorWarning
}

# Get script directory and project paths
$ScriptDir = $PSScriptRoot
$TestsDir = $ScriptDir
$SolutionDir = Split-Path -Parent $TestsDir

$TestProject = Join-Path (Join-Path $TestsDir "SteamNetworkLib.Tests") "SteamNetworkLib.Tests.csproj"
$WorkerProject = Join-Path (Join-Path $TestsDir "SteamNetworkLib.P2PWorker") "SteamNetworkLib.P2PWorker.csproj"

Write-Status "SteamNetworkLib P2P Test Automation"
Write-Status "===================================="
Write-Status "Test Directory: $TestsDir"
Write-Status "Configuration: $Configuration"
Write-Status ""

# Step 1: Validate Directory.Build.user.props
function Test-Configuration {
    Write-Status "Step 1: Validating Configuration..."
    
    $UserProps = Join-Path $SolutionDir "Directory.Build.user.props"
    $TemplateProps = Join-Path $SolutionDir "Directory.Build.user.props.template"
    
    if (-not (Test-Path $UserProps)) {
        Write-Error "Directory.Build.user.props not found!"
        Write-Status "Creating from template..."
        
        if (Test-Path $TemplateProps) {
            Copy-Item $TemplateProps $UserProps
            Write-Warning "Please edit $UserProps and set your game installation path"
            Write-Status "Template created. Please configure and re-run."
            return $false
        } else {
            Write-Error "Template file not found: $TemplateProps"
            return $false
        }
    }
    
    # Parse the props file to check for required values
    $propsContent = Get-Content $UserProps -Raw
    
    if ($propsContent -match '<MonoGameInstallPath>(.*?)</MonoGameInstallPath>') {
        $gamePath = $Matches[1].Trim()
        if ($gamePath -match 'Path\\To\\Your\\Game' -or $gamePath -eq '') {
            Write-Error "MonoGameInstallPath is not configured in Directory.Build.user.props"
            Write-Status "Please set a valid game installation path"
            return $false
        }
        
        if (-not (Test-Path $gamePath)) {
            Write-Error "Game installation path does not exist: $gamePath"
            return $false
        }
        
        Write-Success "Game installation path: $gamePath"
    } else {
        Write-Error "MonoGameInstallPath not found in Directory.Build.user.props"
        return $false
    }
    
    # Check for steam_api64.dll
    $steamApiPaths = @(
        (Join-Path $gamePath "steam_api64.dll"),
        (Join-Path (Join-Path (Join-Path (Join-Path $gamePath "Schedule I_Data") "Plugins") "x86_64") "steam_api64.dll")
    )
    
    $foundSteamApi = $false
    foreach ($path in $steamApiPaths) {
        if (Test-Path $path) {
            Write-Success "Found steam_api64.dll at: $path"
            $foundSteamApi = $true
            break
        }
    }
    
    if (-not $foundSteamApi) {
        Write-Warning "steam_api64.dll not found in expected locations"
        Write-Status "Tests may fail if Goldberg emulator is not installed"
    }
    
    # Check for Steamworks.NET
    $steamworksPath = Join-Path (Join-Path (Join-Path $gamePath "Schedule I_Data") "Managed") "com.rlabrecque.steamworks.net.dll"
    if (Test-Path $steamworksPath) {
        Write-Success "Found Steamworks.NET at: $steamworksPath"
    } else {
        Write-Error "Steamworks.NET not found at: $steamworksPath"
        return $false
    }
    
    return $true
}

# Step 2: Build projects
function Build-Projects {
    Write-Status "Step 2: Building Projects..."
    
    # Build test project
    Write-Status "Building test project..."
    $buildArgs = @(
        "build",
        $TestProject,
        "-c", $Configuration
    )
    
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Test project build failed"
        return $false
    }
    Write-Success "Test project built successfully"
    
    # Build worker project
    Write-Status "Building P2P worker project..."
    $workerArgs = @(
        "build",
        $WorkerProject,
        "-c", $Configuration
    )
    
    & dotnet @workerArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Worker project build failed"
        return $false
    }
    Write-Success "Worker project built successfully"
    
    return $true
}

# Step 3: Run tests
function Invoke-Tests {
    Write-Status "Step 3: Running Tests..."
    
    $testArgs = @(
        "test",
        $TestProject,
        "-c", $Configuration,
        "--no-build"
    )
    
    if ($TestFilter) {
        $testArgs += "--filter"
        $testArgs += $TestFilter
        Write-Status "Filter: $TestFilter"
    }
    
    if ($VerboseOutput) {
        $testArgs += "--logger"
        $testArgs += "console;verbosity=detailed"
    } else {
        $testArgs += "--logger"
        $testArgs += "console;verbosity=normal"
    }
    
    $testArgs += "--blame-hang-timeout"
    $testArgs += "${Timeout}s"
    
    Write-Status "Executing: dotnet $($testArgs -join ' ')"
    Write-Status ""
    
    & dotnet @testArgs
    $script:ExitCode = $LASTEXITCODE
    
    if ($script:ExitCode -eq 0) {
        Write-Success "All tests passed!"
    } else {
        Write-Error "Tests failed with exit code: $script:ExitCode"
    }
    
    return $script:ExitCode
}

# Step 4: Cleanup
function Clear-TestArtifacts {
    Write-Status "Step 4: Cleaning up test artifacts..."
    
    # Clean up temp directories that might be left behind
    $tempBase = Join-Path $env:TEMP "SteamNetworkLib.P2P"
    if (Test-Path $tempBase) {
        try {
            Remove-Item $tempBase -Recurse -Force -ErrorAction SilentlyContinue
            Write-Success "Cleaned up temp directory: $tempBase"
        } catch {
            Write-Warning "Could not fully clean up temp directory (processes may still be running)"
        }
    }
}

# Main execution
if (-not (Test-Configuration)) {
    exit 1
}

if ($ValidateOnly) {
    Write-Success "Configuration validation complete"
    exit 0
}

if (-not $SkipBuild) {
    if (-not (Build-Projects)) {
        exit 1
    }
} else {
    Write-Status "Skipping build step (-SkipBuild specified)"
}

Invoke-Tests
Clear-TestArtifacts

exit $script:ExitCode
