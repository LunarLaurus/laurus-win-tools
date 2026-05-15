<#
.SYNOPSIS
    Builds and installs laurus-win-tools apps on Windows.
.DESCRIPTION
    Builds selected apps in Release mode, copies them to the install directory,
    and optionally registers them to start with Windows.

    Requires .NET 8 SDK to build. Apps run against the .NET 8 Windows Desktop Runtime
    (framework-dependent; no .NET bundled in the install).
.PARAMETER Apps
    One or more app names to install. Defaults to all five.
    Valid values: BatteryTray, ClipTray, NetProfileSwitcher, ProgramHider, SoundTracker
.PARAMETER AutoRun
    Register each installed app to start automatically at Windows login
    (writes to HKCU\...\Run -- no elevation required).
.PARAMETER InstallRoot
    Root directory for installed apps. Each app gets its own subfolder.
    Default: %LOCALAPPDATA%\LaurusWinTools
.PARAMETER Uninstall
    Remove the specified apps from the install directory and autorun registry.
.PARAMETER Run
    Launch each installed app immediately after installation (no delay).
    Useful for first install and updates -- without this you'd need to log out
    and back in (or launch each .exe manually) before they appear in the tray.
.EXAMPLE
    .\install.ps1
.EXAMPLE
    .\install.ps1 -AutoRun -Run
.EXAMPLE
    .\install.ps1 -Apps BatteryTray, SoundTracker -AutoRun
.EXAMPLE
    .\install.ps1 -Apps ProgramHider -Uninstall
#>

[CmdletBinding(DefaultParameterSetName = 'Install', SupportsShouldProcess)]
param(
    [Parameter(ParameterSetName = 'Install')]
    [Parameter(ParameterSetName = 'Uninstall')]
    [ValidateSet('BatteryTray', 'ClipTray', 'NetProfileSwitcher', 'ProgramHider', 'SoundTracker')]
    [string[]]$Apps = @('BatteryTray', 'ClipTray', 'NetProfileSwitcher', 'ProgramHider', 'SoundTracker'),

    [Parameter(ParameterSetName = 'Install')]
    [switch]$AutoRun,

    [Parameter(ParameterSetName = 'Install')]
    [switch]$Run,

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
        StartupArgs = '--startup --delay=5'
    }
    ClipTray = @{
        Project     = 'apps\ClipTray\ClipTray\ClipTray.csproj'
        ExeName     = 'ClipTray.exe'
        StartupArgs = '--startup --delay=5'
    }
    NetProfileSwitcher = @{
        Project     = 'apps\NetProfileSwitcher\NetProfileSwitcher.csproj'
        ExeName     = 'NetProfileSwitcher.exe'
        StartupArgs = '--startup --delay=5'
    }
    ProgramHider = @{
        Project     = 'apps\ProgramHider\app\ProgramHider\ProgramHider.csproj'
        ExeName     = 'ProgramHider.exe'
        StartupArgs = '--startup --delay=5'
    }
    SoundTracker = @{
        Project     = 'apps\SoundTracker\SoundTracker.App\SoundTracker.App.csproj'
        ExeName     = 'SoundTracker.exe'
        StartupArgs = '--startup --delay=5'
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
        Write-Err '.NET SDK not found on PATH. Required to build from source.'
        Write-Err 'Download from https://dot.net/download'
        exit 1
    }
    $sdkVersion = dotnet --version 2>$null
    if ($sdkVersion -notmatch '^8\.') {
        Write-Err ".NET 8 SDK required to build (found: $sdkVersion)."
        Write-Err 'Download from https://dot.net/download'
        exit 1
    }
    Write-Info ".NET SDK $sdkVersion"
}

function Assert-DesktopRuntime {
    $runtimes = & dotnet --list-runtimes 2>$null
    $found = $runtimes | Where-Object { $_ -match 'Microsoft\.WindowsDesktop\.App 8\.' }
    if (-not $found) {
        Write-Info 'Warning: .NET 8 Windows Desktop Runtime not detected on this machine.'
        Write-Info 'Installed apps will not run without it.'
        Write-Info 'Download from https://dot.net/download (choose "Desktop Runtime")'
    }
}

function Wait-ForProcessExit([string]$ProcName, [int]$TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Process -Name $ProcName -ErrorAction SilentlyContinue) -and ((Get-Date) -lt $deadline)) {
        Start-Sleep -Milliseconds 200
    }
    return -not (Get-Process -Name $ProcName -ErrorAction SilentlyContinue)
}

function Stop-AppIfRunning([string]$ExeName) {
    $procName = [System.IO.Path]::GetFileNameWithoutExtension($ExeName)
    $procs = Get-Process -Name $procName -ErrorAction SilentlyContinue
    if (-not $procs) { return $true }

    # First attempt: normal Stop-Process. Works for any process the current
    # user can kill (same integrity level). Fails silently when the target
    # runs elevated and the installer doesn't.
    $procs | Stop-Process -Force -ErrorAction SilentlyContinue

    if (Wait-ForProcessExit $procName -TimeoutSeconds 5) {
        Write-Info "Stopped running instance of $procName"
        # Even after the PID is gone, the kernel may briefly hold the .exe /
        # .dll file handles. A 250ms pause clears the common cases without
        # slowing the happy path.
        Start-Sleep -Milliseconds 250
        return $true
    }

    # Still running -- the target is elevated and we are not. Spawn an
    # elevated taskkill via UAC. Cancelling the prompt throws and we skip
    # this app rather than corrupting the install dir.
    Write-Info "$procName is still running (likely elevated). Requesting elevation to terminate..."
    try {
        $killer = Start-Process -FilePath "taskkill.exe" `
                                 -ArgumentList "/F", "/IM", $ExeName, "/T" `
                                 -Verb RunAs `
                                 -WindowStyle Hidden `
                                 -PassThru `
                                 -ErrorAction Stop
        $killer.WaitForExit(10000) | Out-Null
    }
    catch {
        Write-Err "Elevation denied or failed for $procName : $($_.Exception.Message)"
        return $false
    }

    if (Wait-ForProcessExit $procName -TimeoutSeconds 5) {
        Write-Info "Stopped elevated instance of $procName via UAC"
        Start-Sleep -Milliseconds 250
        return $true
    }

    Write-Err "$procName remained running after elevated taskkill -- aborting install for this app."
    return $false
}

# ---------------------------------------------------------------------------
# Uninstall
# ---------------------------------------------------------------------------
if ($Uninstall) {
    foreach ($appName in $Apps) {
        Write-Step "Uninstalling $appName"
        $def        = $AppDefs[$appName]
        $installDir = Join-Path $InstallRoot $appName

        Stop-AppIfRunning $def.ExeName

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
Assert-DesktopRuntime

Write-Host "`nlaurus-win-tools installer" -ForegroundColor White
Write-Host "Install root : $InstallRoot"
Write-Host "Apps         : $($Apps -join ', ')"
Write-Host "AutoRun      : $($AutoRun.IsPresent)"
Write-Host "Run          : $($Run.IsPresent)"

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

    # Stop any running instance before overwriting
    $stopped = Stop-AppIfRunning $def.ExeName
    if ($stopped -eq $false) {
        Write-Err "$appName is still running and could not be stopped. Quit it manually and re-run."
        continue
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
        $runValue = "`"$exePath`" $($def.StartupArgs)"
        Set-ItemProperty -Path $RunKeyPath -Name $appName -Value $runValue
        Write-Ok "AutoRun: $runValue"
    }
}

if ($Run) {
    Write-Step 'Launching installed apps'
    foreach ($appName in $Apps) {
        $def     = $AppDefs[$appName]
        $exePath = Join-Path (Join-Path $InstallRoot $appName) $def.ExeName
        if (-not (Test-Path $exePath)) {
            Write-Info "$appName not found at $exePath -- skipping"
            continue
        }
        try {
            Start-Process $exePath -ErrorAction Stop | Out-Null
            Write-Ok "Launched $appName"
        }
        catch {
            Write-Err "Failed to launch $appName : $($_.Exception.Message)"
        }
    }
}

Write-Host "`nInstallation complete." -ForegroundColor Green
