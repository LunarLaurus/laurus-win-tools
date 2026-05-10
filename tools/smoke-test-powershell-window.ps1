param(
    [string]$WindowTitle = "Program Hider PowerShell Smoke",
    [string]$ProcessName = "powershell"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$testHostExe = Join-Path $repoRoot "tests\ProgramHider.TestHost\bin\Release\net8.0-windows\ProgramHider.TestHost.exe"
$startedProcess = $null
$windowReady = $false

if (-not (Test-Path $testHostExe)) {
    throw "Test host exe not found at $testHostExe. Build the solution first."
}

try {
    $command = "`$host.UI.RawUI.WindowTitle = '$WindowTitle'"
    $startedProcess = Start-Process -FilePath "powershell.exe" -ArgumentList @(
        "-NoExit",
        "-Command",
        $command
    ) -PassThru

    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        & $testHostExe find-window --title $WindowTitle --process $ProcessName | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $windowReady = $true
            break
        }

        Start-Sleep -Milliseconds 500
    }

    if (-not $windowReady) {
        Write-Host "Window not found after launch. Current manageable windows:"
        & $testHostExe list-windows
        throw "Unable to find the PowerShell smoke target window titled '$WindowTitle'."
    }

    & $testHostExe smoke-hide-restore --title $WindowTitle --process $ProcessName
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "PowerShell smoke test failed with exit code $exitCode."
    }
}
finally {
    if ($startedProcess -and (Get-Process -Id $startedProcess.Id -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $startedProcess.Id -Force
    }
}
