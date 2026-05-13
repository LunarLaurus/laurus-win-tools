<#
.SYNOPSIS
    Builds and installs laurus-win-tools apps on Windows.
.DESCRIPTION
    Builds selected apps in Release mode, copies them to the install directory,
    and optionally registers them to start with Windows.

    Requires .NET 8 SDK. Apps run against the .NET 8 Windows Desktop Runtime
    (framework-dependent; no .NET bundled in the install).
.PARAMETER Apps
    One or more app names to install. Defaults to all four.
    Valid values: BatteryTray, NetProfileSwitcher, ProgramHider, SoundTracker
.PARAMETER AutoRun
    Register each installed app to start automatically at Windows login
    (writes to HKCU\...\Run -- no elevation required).
.PARAMETER InstallRoot
    Root directory for installed apps. Each app gets its own subfolder.
    Default: %LOCALAPPDATA%\LaurusWinTools
.PARAMETER Uninstall
    Remove the specified apps from the install directory and autorun registry.
.EXAMPLE
    .\install.ps1
.EXAMPLE
    .\install.ps1 -AutoRun
.EXAMPLE
    .\install.ps1 -Apps BatteryTray, SoundTracker -AutoRun
.EXAMPLE
    .\install.ps1 -Apps ProgramHider -Uninstall
#>

[CmdletBinding(DefaultParameterSetName = 'Install', SupportsShouldProcess)]
param(
    [Parameter(ParameterSetName = 'Install')]
    [Parameter(ParameterSetName = 'Uninstall')]
    [ValidateSet('BatteryTray', 'NetProfileSwitcher', 'ProgramHider', 'SoundTracker')]
    [string[]]$Apps = @('BatteryTray', 'NetProfileSwitcher', 'ProgramHider', 'SoundTracker'),

    [Parameter(ParameterSetName = 'Install')]
    [switch]$AutoRun,

    [Parameter(ParameterSetName = 'Install')]
    [Parameter(ParameterSetName = 'Uninstall')]
    [string]$InstallRoot = "$env:LOCALAPPDATA\LaurusWinTools",

    [Parameter(ParameterSetName = 'Uninstall')]
    [switch]$Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot   = $PSScriptRoot
$RunKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

# ---------------------------------------------------------------------------
# App definitions
# ---------------------------------------------------------------------------
$AppDefs = @{
    BatteryTray = @{
        Project     = 'apps\BatteryTray\BatteryTray\BatteryTray.csproj'
        ExeName     = 'BatteryTray.exe'
        StartupArgs = ''
    }
    NetProfileSwitcher = @{
        Project     = 'apps\NetProfileSwitcher\NetProfileSwitcher.csproj'
        ExeName     = 'NetProfileSwitcher.exe'
        StartupArgs = ''
    }
    ProgramHider = @{
        Project     = 'apps\ProgramHider\app\ProgramHider\ProgramHider.csproj'
        ExeName     = 'ProgramHider.exe'
        StartupArgs = '--startup --delay=5'
    }
    SoundTracker = @{
        Project     = 'apps\SoundTracker\SoundTracker.App\SoundTracker.App.csproj'
        ExeName     = 'SoundTracker.exe'
        StartupArgs = ''
    }
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
function Write-Step([string]$Msg) { Write-Host "`n==> $Msg"         -ForegroundColor Cyan  }
function Write-Ok([string]$Msg)   { Write-Host "    [ok]   $Msg"    -ForegroundColor Green }
function Write-Info([string]$Msg) { Write-Host "    [info] $Msg"    -ForegroundColor Gray  }
function Write-Err([string]$Msg)  { Write-Host "    [fail] $Msg"    -ForegroundColor Red   }

function Assert-Dotnet {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Err '.NET SDK not found on PATH. Download from https://dot.net'
        exit 1
    }
    $sdkVersion = dotnet --version 2>$null
    if ($sdkVersion -notmatch '^8\.') {
        Write-Err ".NET 8 SDK required (found: $sdkVersion). Download from https://dot.net"
        exit 1
    }
    Write-Info ".NET SDK $sdkVersion"
}

# ---------------------------------------------------------------------------
# Uninstall
# ---------------------------------------------------------------------------
if ($Uninstall) {
    foreach ($appName in $Apps) {
        Write-Step "Uninstalling $appName"
        $installDir = Join-Path $InstallRoot $appName

        if (Test-Path $installDir) {
            Remove-Item $installDir -Recurse -Force
            Write-Ok "Removed $installDir"
        }
        else {
            Write-Info "$installDir not found -- skipping"
        }

        $existing = Get-ItemProperty -Path $RunKeyPath -Name $appName -ErrorAction SilentlyContinue
        if ($null -ne $existing) {
            Remove-ItemProperty -Path $RunKeyPath -Name $appName -Force
            Write-Ok 'Removed autorun entry'
        }
    }
    Write-Host "`nUninstall complete." -ForegroundColor Green
    exit 0
}

# ---------------------------------------------------------------------------
# Install
# ---------------------------------------------------------------------------
Assert-Dotnet

Write-Host "`nlaurus-win-tools installer" -ForegroundColor White
Write-Host "Install root : $InstallRoot"
Write-Host "Apps         : $($Apps -join ', ')"
Write-Host "AutoRun      : $($AutoRun.IsPresent)"

if (-not (Test-Path $InstallRoot)) {
    New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
}

foreach ($appName in $Apps) {
    $def         = $AppDefs[$appName]
    $installDir  = Join-Path $InstallRoot $appName
    $projectPath = Join-Path $RepoRoot $def.Project
    $guidStr     = [System.Guid]::NewGuid().ToString('N')
    $publishDir  = Join-Path ([System.IO.Path]::GetTempPath()) "laurus-pub-$appName-$guidStr"

    Write-Step $appName

    # Build and publish to a temp directory
    Write-Info 'Building (Release)...'
    & dotnet publish $projectPath --configuration Release --no-self-contained --output $publishDir --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Err "dotnet publish failed for $appName (exit $LASTEXITCODE)"
        exit 1
    }

    # Swap into install directory
    if (Test-Path $installDir) {
        Remove-Item $installDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Copy-Item "$publishDir\*" $installDir -Recurse -Force
    Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    Write-Ok "Installed to $installDir"

    # AutoRun -- writes to HKCU Run key, no elevation required
    if ($AutoRun) {
        $exePath = Join-Path $installDir $def.ExeName
        if ($def.StartupArgs) {
            $runValue = "`"$exePath`" $($def.StartupArgs)"
        }
        else {
            $runValue = "`"$exePath`""
        }
        Set-ItemProperty -Path $RunKeyPath -Name $appName -Value $runValue
        Write-Ok "AutoRun: $runValue"
    }
}

Write-Host "`nInstallation complete." -ForegroundColor Green
