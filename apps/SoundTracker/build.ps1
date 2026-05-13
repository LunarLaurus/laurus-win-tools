param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$RunSmokeTests = $true,
    [switch]$StopRunningProcesses
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $repoRoot "SoundTracker.sln"
$smokeExePath = Join-Path $repoRoot "SoundTracker.SmokeTests\bin\$Configuration\net8.0-windows10.0.19041.0\SoundTracker.SmokeTests.exe"
$processNames = @("SoundTracker", "SoundTracker.SmokeTests")
$mutexName = "Global\SoundTracker.Build"

$mutex = New-Object System.Threading.Mutex($false, $mutexName)
$lockTaken = $false

try {
    try {
        $lockTaken = $mutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        $lockTaken = $true
    }

    if (-not $lockTaken) {
        throw "Another SoundTracker build is already running."
    }

    $running = Get-Process -Name $processNames -ErrorAction SilentlyContinue
    if ($running) {
        if ($StopRunningProcesses) {
            $running | Stop-Process -Force
            Start-Sleep -Milliseconds 500
        }
        else {
            $summary = ($running | ForEach-Object { "$($_.ProcessName)($($_.Id))" }) -join ", "
            throw "Build blocked by running processes: $summary. Rerun with -StopRunningProcesses to stop them first."
        }
    }

    Push-Location $repoRoot
    try {
        Write-Host "Building $solutionPath ($Configuration)..."
        & dotnet build $solutionPath -c $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE."
        }

        if ($RunSmokeTests) {
            if (-not (Test-Path $smokeExePath)) {
                throw "Smoke test executable not found at $smokeExePath"
            }

            Write-Host "Running smoke tests..."
            & $smokeExePath
            if ($LASTEXITCODE -ne 0) {
                throw "Smoke tests failed with exit code $LASTEXITCODE."
            }
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($lockTaken) {
        $mutex.ReleaseMutex() | Out-Null
    }

    $mutex.Dispose()
}
