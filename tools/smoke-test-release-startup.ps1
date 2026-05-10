param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [int]$WaitSeconds = 3
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
    throw "Release executable not found at $ExePath"
}

$process = Start-Process -FilePath $ExePath -PassThru
try {
    Start-Sleep -Seconds $WaitSeconds
    if (-not (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) {
        throw "ProgramHider.exe exited before the startup smoke check completed."
    }

    Write-Host "startup-smoke-ok:$($process.Id)"
}
finally {
    if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) {
        Stop-Process -Id $process.Id -Force
    }
}
