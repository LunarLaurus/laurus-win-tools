<#
.SYNOPSIS
    Launches all installed laurus-win-tools apps.
.DESCRIPTION
    Iterates each known app, finds its executable under the install root,
    and starts it if it's not already running. Skips missing installs and
    already-running processes silently (with an info line).

    Use this when the apps are installed but not yet running -- e.g. after
    `install.ps1` without `-Run`, or to relaunch the tray after a sign-out
    cycle when AutoRun isn't enabled.
.PARAMETER Apps
    One or more app names to launch. Defaults to all four.
.PARAMETER InstallRoot
    Root directory where the apps are installed.
    Default: %LOCALAPPDATA%\LaurusWinTools
.EXAMPLE
    .\run-all.ps1
.EXAMPLE
    .\run-all.ps1 -Apps BatteryTray, SoundTracker
#>

[CmdletBinding()]
param(
    [ValidateSet('BatteryTray', 'NetProfileSwitcher', 'ProgramHider', 'SoundTracker')]
    [string[]]$Apps = @('BatteryTray', 'NetProfileSwitcher', 'ProgramHider', 'SoundTracker'),

    [string]$InstallRoot = "$env:LOCALAPPDATA\LaurusWinTools"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExeNames = @{
    BatteryTray        = 'BatteryTray.exe'
    NetProfileSwitcher = 'NetProfileSwitcher.exe'
    ProgramHider       = 'ProgramHider.exe'
    SoundTracker       = 'SoundTracker.exe'
}

function Write-Step([string]$Msg) { Write-Host "`n==> $Msg"      -ForegroundColor Cyan  }
function Write-Ok([string]$Msg)   { Write-Host "    [ok]   $Msg" -ForegroundColor Green }
function Write-Info([string]$Msg) { Write-Host "    [info] $Msg" -ForegroundColor Gray  }
function Write-Err([string]$Msg)  { Write-Host "    [fail] $Msg" -ForegroundColor Red   }

Write-Host "Launching laurus-win-tools apps" -ForegroundColor White
Write-Host "Install root : $InstallRoot"
Write-Host "Apps         : $($Apps -join ', ')"

foreach ($appName in $Apps) {
    Write-Step $appName
    $exeName  = $ExeNames[$appName]
    $procName = [System.IO.Path]::GetFileNameWithoutExtension($exeName)
    $exePath  = Join-Path (Join-Path $InstallRoot $appName) $exeName

    if (Get-Process -Name $procName -ErrorAction SilentlyContinue) {
        Write-Info 'Already running -- skipping'
        continue
    }

    if (-not (Test-Path $exePath)) {
        Write-Info "Not installed at $exePath -- skipping"
        continue
    }

    try {
        Start-Process $exePath -ErrorAction Stop | Out-Null
        Write-Ok "Launched ($exePath)"
    }
    catch {
        Write-Err "Failed: $($_.Exception.Message)"
    }
}

Write-Host "`nDone." -ForegroundColor Green
