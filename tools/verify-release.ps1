param(
    [string]$DotNetPath = "D:\tooling\dotnet\dotnet.exe",
    [switch]$SkipLiveSmoke
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    # Keep this script focused on repo-owned targets so routine verification
    # never depends on arbitrary third-party windows being open.
    Write-Host "Running Program Hider verification harness..."
    & $DotNetPath run --project tests\ProgramHider.TestHost\ProgramHider.TestHost.csproj -c Release

    if ($SkipLiveSmoke) {
        Write-Host "Skipping live smoke tests."
        return
    }

    Write-Host "Running sample-window smoke test..."
    & powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-sample-window.ps1

    Write-Host "Running PowerShell smoke test..."
    & powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-powershell-window.ps1

    Write-Host "Running hotkey smoke test..."
    & powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-program-hider-hotkey.ps1
}
finally {
    Pop-Location
}
