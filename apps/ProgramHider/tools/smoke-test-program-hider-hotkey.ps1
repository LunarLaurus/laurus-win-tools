param(
    [string]$WindowTitle = "Program Hider Hotkey Smoke",
    [string]$ProcessName = "ProgramHider.SmokeWindow"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$programHiderExe = Join-Path $repoRoot "app\ProgramHider\bin\Release\net8.0-windows\win-x64\ProgramHider.exe"
$sampleExe = Join-Path $repoRoot "tests\ProgramHider.SmokeWindow\bin\Release\net8.0-windows\ProgramHider.SmokeWindow.exe"
$testHostExe = Join-Path $repoRoot "tests\ProgramHider.TestHost\bin\Release\net8.0-windows\ProgramHider.TestHost.exe"
$tempSettingsPath = Join-Path $env:TEMP "program-hider-hotkey-smoke-settings.json"
$programHider = $null
$sampleWindow = $null

if (-not (Test-Path $programHiderExe)) {
    throw "Program Hider exe not found at $programHiderExe. Build the app first."
}

if (-not (Test-Path $sampleExe)) {
    throw "Smoke target exe not found at $sampleExe. Build the smoke window first."
}

if (-not (Test-Path $testHostExe)) {
    throw "Test host exe not found at $testHostExe. Build the test host first."
}

try {
    @'
{
  "Hotkey": {
    "Control": true,
    "Shift": true,
    "Alt": false,
    "Windows": false,
    "Key": "H"
  },
  "LaunchOnWindowsStartup": false,
  "StartupDelaySeconds": 0,
  "RestoreWithoutFocus": false,
  "RequirePinToRestore": false,
  "PinHash": "",
  "RestoreAllPinHash": "",
  "UnlockTimeoutMinutes": 5,
  "RestoreHiddenWindowsOnSessionLock": false,
  "RestoreHiddenWindowsOnSuspend": false,
  "WindowRules": []
}
'@ | Set-Content -Path $tempSettingsPath -Encoding UTF8

    $env:PROGRAM_HIDER_SETTINGS_PATH = $tempSettingsPath
    $programHider = Start-Process -FilePath $programHiderExe -PassThru
    Start-Sleep -Seconds 2

    $sampleWindow = Start-Process -FilePath $sampleExe -ArgumentList @($WindowTitle) -PassThru
    Start-Sleep -Seconds 2

    & $testHostExe find-window --title $WindowTitle --process $ProcessName | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to confirm that the sample window is visible before the hotkey smoke."
    }

    & $testHostExe trigger-program-hider-hotkey | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to post the hotkey message to Program Hider."
    }

    $hidden = $false
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        Start-Sleep -Milliseconds 500
        & $testHostExe find-window --title $WindowTitle --process $ProcessName | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $hidden = $true
            break
        }
    }

    if (-not $hidden) {
        throw "Hotkey hide failed: the sample window is still visible."
    }

    Write-Host "hotkey-hide-verify-ok"
}
finally {
    Remove-Item Env:PROGRAM_HIDER_SETTINGS_PATH -ErrorAction SilentlyContinue

    if ($programHider -and (Get-Process -Id $programHider.Id -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $programHider.Id -Force
    }

    if ($sampleWindow -and (Get-Process -Id $sampleWindow.Id -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $sampleWindow.Id -Force
    }

    Remove-Item $tempSettingsPath -ErrorAction SilentlyContinue
}
