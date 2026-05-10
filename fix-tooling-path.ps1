param(
    [switch]$WhatIf,
    [switch]$SkipVerification
)

$ErrorActionPreference = "Stop"

function Split-PathList {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return @()
    }

    return $Value -split ";" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}

function Merge-PreferredPathEntries {
    param(
        [string[]]$Preferred,
        [string[]]$Existing
    )

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $merged = [System.Collections.Generic.List[string]]::new()

    foreach ($entry in ($Preferred + $Existing)) {
        if (-not [string]::IsNullOrWhiteSpace($entry) -and $seen.Add($entry)) {
            [void]$merged.Add($entry)
        }
    }

    return $merged
}

function Get-EffectiveRegistryPath {
    param(
        [string]$MachinePath,
        [string]$UserPath
    )

    if ([string]::IsNullOrWhiteSpace($UserPath)) {
        return $MachinePath
    }

    return "$MachinePath;$UserPath"
}

function Send-EnvironmentChangeBroadcast {
    if (-not ("ToolingPath.User32" -as [type])) {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

namespace ToolingPath
{
    public static class User32
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            UIntPtr wParam,
            string lParam,
            uint fuFlags,
            uint uTimeout,
            out UIntPtr lpdwResult);
    }
}
"@
    }

    $HWND_BROADCAST = [IntPtr]0xffff
    $WM_SETTINGCHANGE = 0x001A
    $SMTO_ABORTIFHUNG = 0x0002
    $result = [UIntPtr]::Zero

    [void][ToolingPath.User32]::SendMessageTimeout(
        $HWND_BROADCAST,
        $WM_SETTINGCHANGE,
        [UIntPtr]::Zero,
        "Environment",
        $SMTO_ABORTIFHUNG,
        5000,
        [ref]$result
    )
}

$preferred = @(
    "D:\tooling\dotnet",
    "D:\tooling\go\bin",
    "D:\tooling\rust\.cargo\bin",
    "D:\tooling\miniconda3",
    "D:\tooling\miniconda3\Scripts",
    "D:\tooling\miniconda3\Library\bin",
    "D:\tooling\miniconda3\condabin",
    "D:\tooling\npm-global"
)

$machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")

$newMachineSegments = Merge-PreferredPathEntries -Preferred $preferred -Existing (Split-PathList $machinePath)
$newMachinePath = $newMachineSegments -join ";"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = Join-Path "D:\tmp" "tooling-path-backup-$timestamp.txt"
@(
    "MACHINE_PATH=$machinePath"
    "USER_PATH=$userPath"
) | Set-Content -Path $backupPath

Write-Host "Backup written to $backupPath"
Write-Host ""
Write-Host "Preferred tooling entries:"
$preferred | ForEach-Object { Write-Host "  $_" }
Write-Host ""

$missingPreferred = $preferred | Where-Object { -not (Test-Path $_) }
if ($missingPreferred) {
    Write-Warning "Some preferred PATH entries do not currently exist:"
    $missingPreferred | ForEach-Object { Write-Warning "  $_" }
    Write-Host ""
}

if ($WhatIf) {
    Write-Host "WhatIf: machine PATH would be updated to:"
    Write-Host $newMachinePath
    exit 0
}

$principal = [Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Administrator rights are required to update the machine PATH."
}

[Environment]::SetEnvironmentVariable("Path", $newMachinePath, "Machine")
$env:Path = Get-EffectiveRegistryPath -MachinePath $newMachinePath -UserPath $userPath
Send-EnvironmentChangeBroadcast

Write-Host "Updated machine PATH."
Write-Host ""

if (-not $SkipVerification) {
    $verifyScript = Join-Path $PSScriptRoot "verify-tooling-path.ps1"
    if (Test-Path $verifyScript) {
        Write-Host "Fresh-shell verification:"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $verifyScript -FreshShell
    }
    else {
        Write-Warning "Verification script not found at $verifyScript"
    }
}
