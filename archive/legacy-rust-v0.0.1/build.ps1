param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$cargoToml = Join-Path $repoRoot "Cargo.toml"
$versionLine = Select-String -Path $cargoToml -Pattern '^version\s*=\s*"([^"]+)"$' | Select-Object -First 1

if (-not $versionLine) {
    throw "Unable to read version from Cargo.toml"
}

$version = $versionLine.Matches[0].Groups[1].Value
$cargo = (Get-Command cargo -ErrorAction Stop).Source
$env:CARGO_TARGET_DIR = Join-Path $repoRoot "target"

Push-Location $repoRoot
try {
    & $cargo build --release

    $sourceExe = Join-Path $repoRoot "target\release\ProgramHider.exe"
    if (-not (Test-Path $sourceExe)) {
        throw "Expected build output not found at $sourceExe"
    }

    $releaseDir = Join-Path $repoRoot "release\v$version"
    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

    $destExe = Join-Path $releaseDir "ProgramHider.exe"
    Copy-Item -Path $sourceExe -Destination $destExe -Force

    Write-Host "Packaged executable: $destExe"
}
finally {
    Pop-Location
}
