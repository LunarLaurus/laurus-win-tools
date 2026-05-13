param(
    [switch]$FreshShell,
    [switch]$FreshShellChild,
    [string]$ReportPath
)

$ErrorActionPreference = "Stop"

function Get-EffectiveRegistryPath {
    $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")

    if ([string]::IsNullOrWhiteSpace($userPath)) {
        return $machinePath
    }

    return "$machinePath;$userPath"
}

function Write-ToolResolution {
    param([string]$Tool)

    Write-Host "=== $Tool : where.exe ==="
    $whereOutput = & where.exe $Tool 2>&1
    if ($whereOutput) {
        $whereOutput | ForEach-Object { Write-Host $_ }
    }
    else {
        Write-Host "(not found)"
    }

    Write-Host "=== $Tool : Get-Command ==="
    $commands = Get-Command $Tool -All -ErrorAction SilentlyContinue |
        Select-Object Name, Source, CommandType

    if ($commands) {
        $commands | Format-Table -AutoSize
    }
    else {
        Write-Host "(not found)"
    }

    Write-Host ""
}

if ($FreshShell -and -not $FreshShellChild) {
    if ([string]::IsNullOrWhiteSpace($ReportPath)) {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $ReportPath = Join-Path "D:\tmp" "tooling-path-verify-fresh-$timestamp.txt"
    }

    $childArgs = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $MyInvocation.MyCommand.Path,
        "-FreshShellChild"
    )

    $process = Start-Process powershell `
        -ArgumentList $childArgs `
        -WindowStyle Hidden `
        -Wait `
        -PassThru `
        -RedirectStandardOutput $ReportPath

    if ($process.ExitCode -ne 0) {
        throw "Fresh-shell verification failed with exit code $($process.ExitCode)."
    }

    Get-Content -Path $ReportPath
    return
}

if ($FreshShellChild) {
    # Simulate a newly opened shell by rebuilding PATH from the persisted
    # machine and user registry values instead of inheriting the parent process.
    $env:Path = Get-EffectiveRegistryPath
    Write-Host "[fresh-shell] PATH rebuilt from machine + user registry values."
    Write-Host ""
}

Write-Host "User PATH:"
[Environment]::GetEnvironmentVariable("Path", "User")
Write-Host ""
Write-Host "Machine PATH:"
[Environment]::GetEnvironmentVariable("Path", "Machine")
Write-Host ""
Write-Host "Process PATH:"
$env:Path
Write-Host ""

foreach ($tool in @("dotnet", "go", "cargo", "python", "conda")) {
    Write-ToolResolution -Tool $tool
}
