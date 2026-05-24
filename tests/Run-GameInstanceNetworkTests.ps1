#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs SteamNetworkLib networking tests in two isolated Schedule I game instances.

.DESCRIPTION
    Builds the test Melon mod, copies the configured Mono game install into host/client
    temp directories, writes isolated Goldberg configs, launches both instances, and
    returns pass/fail from per-role result files.
#>

param(
    [string]$GamePath = "",
    [string]$InstanceRoot = "",
    [string]$HostSteamId = "76561199320154780",
    [string]$ClientSteamId = "76561199485712034",
    [int]$HostInitDelaySeconds = 25,
    [int]$TestTimeoutSeconds = 90,
    [switch]$KeepInstances
)

$ErrorActionPreference = "Stop"

function Write-Step($Message) {
    Write-Host $Message -ForegroundColor Yellow
}

function Assert-Path($Path, $Description) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Get-RepoRoot {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
}

function Get-ConfiguredGamePath {
    $repoRoot = Get-RepoRoot
    $propsFiles = @(
        (Join-Path $repoRoot "Directory.Build.user.props"),
        (Join-Path $repoRoot "Directory.Build.props")
    )

    foreach ($propsFile in $propsFiles) {
        if (-not (Test-Path -LiteralPath $propsFile)) {
            continue
        }

        try {
            [xml]$props = Get-Content -LiteralPath $propsFile -Raw
            $monoPath = $props.Project.PropertyGroup.MonoGameInstallPath |
                Select-Object -First 1
            if (-not [string]::IsNullOrWhiteSpace($monoPath)) {
                $expanded = [Environment]::ExpandEnvironmentVariables($monoPath)
                if (Test-Path -LiteralPath $expanded) {
                    return $expanded
                }
            }
        }
        catch {
            Write-Host "Could not read game path from $propsFile`: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }

    throw "GamePath was not provided and no valid MonoGameInstallPath was found in Directory.Build.user.props or Directory.Build.props."
}

function New-FileLinkOrCopy {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    try {
        New-Item -ItemType HardLink -Path $DestinationPath -Target $SourcePath -Force | Out-Null
    }
    catch {
        Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
    }
}

function Copy-GameFiles {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null

    foreach ($file in @("Schedule I.exe", "UnityCrashHandler64.exe", "UnityPlayer.dll", "steam_appid.txt", "version.dll", "GameAssembly.dll", "baselib.dll")) {
        $sourceFile = Join-Path $SourcePath $file
        if (Test-Path -LiteralPath $sourceFile) {
            New-FileLinkOrCopy -SourcePath $sourceFile -DestinationPath (Join-Path $DestinationPath $file)
        }
    }

    $monoRuntime = Join-Path $SourcePath "MonoBleedingEdge"
    if (Test-Path -LiteralPath $monoRuntime) {
        New-Item -ItemType Junction -Path (Join-Path $DestinationPath "MonoBleedingEdge") -Target $monoRuntime -Force | Out-Null
    }

    $sourceData = Join-Path $SourcePath "Schedule I_Data"
    $destData = Join-Path $DestinationPath "Schedule I_Data"
    New-Item -ItemType Directory -Path $destData -Force | Out-Null

    foreach ($item in Get-ChildItem -LiteralPath $sourceData -Force) {
        $destItem = Join-Path $destData $item.Name
        if ($item.PSIsContainer) {
            if ($item.Name -eq "Plugins") {
                Copy-Item -LiteralPath $item.FullName -Destination $destItem -Recurse -Force
            }
            else {
                New-Item -ItemType Junction -Path $destItem -Target $item.FullName -Force | Out-Null
            }
        }
        else {
            New-FileLinkOrCopy -SourcePath $item.FullName -DestinationPath $destItem
        }
    }

    foreach ($dir in @("MelonLoader", "UserLibs")) {
        $sourceDir = Join-Path $SourcePath $dir
        if (Test-Path -LiteralPath $sourceDir) {
            Copy-Item -LiteralPath $sourceDir -Destination (Join-Path $DestinationPath $dir) -Recurse -Force
        }
    }

    New-Item -ItemType Directory -Path (Join-Path $DestinationPath "Mods") -Force | Out-Null
}

function Remove-TestRoot {
    param(
        [string]$RootPath,
        [string]$AllowedBasePath
    )

    $resolvedRoot = [System.IO.Path]::GetFullPath($RootPath)
    $resolvedBase = [System.IO.Path]::GetFullPath($AllowedBasePath)
    if (-not $resolvedRoot.StartsWith($resolvedBase, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean test directory outside expected base: $resolvedRoot"
    }

    Get-ChildItem -LiteralPath $resolvedRoot -Recurse -Force -Attributes ReparsePoint -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        ForEach-Object {
            try {
                [System.IO.Directory]::Delete($_.FullName, $false)
            }
            catch {
                Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
            }
        }

    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force -ErrorAction SilentlyContinue
}

function Set-GoldbergConfig {
    param(
        [string]$InstancePath,
        [string]$SteamId,
        [string]$Name
    )

    $configDir = Join-Path $InstancePath "Schedule I_Data\Plugins\x86_64\steam_settings"
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null

    $configContent = @"
[user::general]
account_name=$Name
account_steamid=$SteamId
language=english
"@

    Set-Content -LiteralPath (Join-Path $configDir "configs.user.ini") -Value $configContent -Encoding UTF8
}

function Stop-TestProcess {
    param($Process, [string]$Name)

    if ($Process -and -not $Process.HasExited) {
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        Write-Host "Stopped $Name process $($Process.Id)" -ForegroundColor Gray
    }
}

if ([string]::IsNullOrWhiteSpace($GamePath)) {
    $GamePath = Get-ConfiguredGamePath
}

Write-Host "SteamNetworkLib isolated real-game networking tests" -ForegroundColor Cyan
Write-Host "GamePath: $GamePath" -ForegroundColor Gray

Assert-Path $GamePath "Game path"
Assert-Path (Join-Path $GamePath "Schedule I.exe") "Schedule I executable"
Assert-Path (Join-Path $GamePath "Schedule I_Data\Plugins\x86_64\steam_api64.dll") "Goldberg steam_api64.dll"

if ([string]::IsNullOrWhiteSpace($InstanceRoot)) {
    $InstanceRoot = Join-Path (Split-Path -Parent $GamePath) "SteamNetworkLib.GameInstances"
}

$testRootBase = $InstanceRoot
$testRoot = Join-Path $testRootBase ([Guid]::NewGuid().ToString("N"))
$hostDir = Join-Path $testRoot "host"
$clientDir = Join-Path $testRoot "client"
$sharedDir = Join-Path $testRoot "shared"
$hostResults = Join-Path $sharedDir "host-results.txt"
$clientResults = Join-Path $sharedDir "client-results.txt"

$hostProcess = $null
$clientProcess = $null

try {
    New-Item -ItemType Directory -Path $sharedDir -Force | Out-Null

    Write-Step "Step 1: Build test mod"
    $testModProject = Join-Path $PSScriptRoot "SteamNetworkLib.TestMod\SteamNetworkLib.TestMod.csproj"
    dotnet build $testModProject -c Mono
    if ($LASTEXITCODE -ne 0) {
        throw "Test mod build failed"
    }

    $testModOutput = Join-Path $PSScriptRoot "SteamNetworkLib.TestMod\bin\Mono\netstandard2.1"
    $testModDll = Join-Path $testModOutput "SteamNetworkLib.TestMod.dll"
    $steamNetworkLibDll = Join-Path $testModOutput "SteamNetworkLib.dll"
    Assert-Path $testModDll "Test mod DLL"
    Assert-Path $steamNetworkLibDll "SteamNetworkLib DLL"

    Write-Step "Step 2: Copy isolated game instances"
    Copy-GameFiles -SourcePath $GamePath -DestinationPath $hostDir
    Copy-GameFiles -SourcePath $GamePath -DestinationPath $clientDir

    foreach ($instance in @($hostDir, $clientDir)) {
        $modsDir = Join-Path $instance "Mods"
        $userLibsDir = Join-Path $instance "UserLibs"
        New-Item -ItemType Directory -Path $userLibsDir -Force | Out-Null
        Copy-Item -LiteralPath $testModDll -Destination $modsDir -Force
        Copy-Item -LiteralPath $steamNetworkLibDll -Destination $userLibsDir -Force
    }

    Write-Step "Step 3: Configure Goldberg"
    Set-GoldbergConfig -InstancePath $hostDir -SteamId $HostSteamId -Name "TestHost"
    Set-GoldbergConfig -InstancePath $clientDir -SteamId $ClientSteamId -Name "TestClient"

    Write-Step "Step 4: Launch host"
    $hostExe = Join-Path $hostDir "Schedule I.exe"
    $hostArgs = "--host --snl-test-dir `"$sharedDir`""
    $hostProcess = Start-Process -FilePath $hostExe -ArgumentList $hostArgs -WorkingDirectory $hostDir -PassThru -WindowStyle Hidden
    Write-Host "Host PID: $($hostProcess.Id)" -ForegroundColor Green

    Start-Sleep -Seconds $HostInitDelaySeconds

    Write-Step "Step 5: Launch client"
    $clientExe = Join-Path $clientDir "Schedule I.exe"
    $clientArgs = "--join --snl-test-dir `"$sharedDir`""
    $clientProcess = Start-Process -FilePath $clientExe -ArgumentList $clientArgs -WorkingDirectory $clientDir -PassThru -WindowStyle Hidden
    Write-Host "Client PID: $($clientProcess.Id)" -ForegroundColor Green

    Write-Step "Step 6: Wait for results"
    $deadline = (Get-Date).AddSeconds($TestTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ((Test-Path -LiteralPath $hostResults) -and (Test-Path -LiteralPath $clientResults)) {
            break
        }

        if ($hostProcess.HasExited -and $clientProcess.HasExited) {
            break
        }

        Start-Sleep -Seconds 2
    }

    $hostText = if (Test-Path -LiteralPath $hostResults) { Get-Content -LiteralPath $hostResults -Raw } else { "HOST|FAIL|No host result" }
    $clientText = if (Test-Path -LiteralPath $clientResults) { Get-Content -LiteralPath $clientResults -Raw } else { "CLIENT|FAIL|No client result" }

    Write-Host "Host result: $hostText" -ForegroundColor Gray
    Write-Host "Client result: $clientText" -ForegroundColor Gray

    if ($hostText -notmatch "HOST\|PASS" -or $clientText -notmatch "CLIENT\|PASS") {
        $hostLog = Join-Path $hostDir "MelonLoader\Latest.log"
        $clientLog = Join-Path $clientDir "MelonLoader\Latest.log"
        Write-Host "Host log: $hostLog" -ForegroundColor Yellow
        Write-Host "Client log: $clientLog" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "All isolated real-game networking tests passed." -ForegroundColor Green
    exit 0
}
finally {
    Stop-TestProcess -Process $hostProcess -Name "host"
    Stop-TestProcess -Process $clientProcess -Name "client"

    if (-not $KeepInstances -and (Test-Path -LiteralPath $testRoot)) {
        Start-Sleep -Seconds 2
        Remove-TestRoot -RootPath $testRoot -AllowedBasePath $testRootBase
    }
}
