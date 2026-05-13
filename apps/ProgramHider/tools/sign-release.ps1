param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,
    [string]$SignToolPath = $env:PROGRAM_HIDER_SIGNTOOL,
    [string]$PfxPath = $env:PROGRAM_HIDER_PFX_PATH,
    [string]$PfxPassword = $env:PROGRAM_HIDER_PFX_PASSWORD,
    [string]$TimestampUrl = $env:PROGRAM_HIDER_TIMESTAMP_URL
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
    throw "Executable not found at $ExePath"
}

if ([string]::IsNullOrWhiteSpace($SignToolPath) -or
    [string]::IsNullOrWhiteSpace($PfxPath) -or
    [string]::IsNullOrWhiteSpace($PfxPassword)) {
    Write-Host "Signing skipped: provide PROGRAM_HIDER_SIGNTOOL, PROGRAM_HIDER_PFX_PATH, and PROGRAM_HIDER_PFX_PASSWORD to enable signing."
    return
}

if (-not (Test-Path $SignToolPath)) {
    throw "Configured signtool not found at $SignToolPath"
}

if (-not (Test-Path $PfxPath)) {
    throw "Configured certificate not found at $PfxPath"
}

$arguments = @(
    "sign",
    "/fd", "SHA256",
    "/f", $PfxPath,
    "/p", $PfxPassword
)

if (-not [string]::IsNullOrWhiteSpace($TimestampUrl)) {
    $arguments += @("/tr", $TimestampUrl, "/td", "SHA256")
}

$arguments += $ExePath

& $SignToolPath @arguments
