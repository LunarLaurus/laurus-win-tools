param(
    [switch]$SkipSigning,
    [switch]$SkipVerification,
    [switch]$SkipLiveSmoke,
    [switch]$SkipStartupSmoke
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot "app\ProgramHider\ProgramHider.csproj"
$dotnet = "D:\tooling\dotnet\dotnet.exe"
$signScript = Join-Path $repoRoot "tools\sign-release.ps1"
$verifyScript = Join-Path $repoRoot "tools\verify-release.ps1"
$startupSmokeScript = Join-Path $repoRoot "tools\smoke-test-release-startup.ps1"

if (-not (Test-Path $dotnet)) {
    throw "Expected .NET SDK host not found at $dotnet"
}

$versionLine = Select-String -Path $projectPath -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
if (-not $versionLine) {
    throw "Unable to read version from $projectPath"
}

$version = $versionLine.Matches[0].Groups[1].Value
$releaseRoot = Join-Path $repoRoot "release"
$releaseDir = Join-Path $releaseRoot "v$version"
$portableZip = Join-Path $releaseRoot "ProgramHider-v$version-portable.zip"

Push-Location $repoRoot
try {
    if (Test-Path $releaseDir) {
        Remove-Item -Recurse -Force $releaseDir
    }

    if (Test-Path $portableZip) {
        Remove-Item -Force $portableZip
    }

    & $dotnet build $projectPath -c Release

    if (-not $SkipVerification) {
        $verifyArgs = @(
            "-ExecutionPolicy", "Bypass",
            "-File", $verifyScript,
            "-DotNetPath", $dotnet
        )
        if ($SkipLiveSmoke) {
            $verifyArgs += "-SkipLiveSmoke"
        }

        & powershell @verifyArgs
    }

    & $dotnet publish $projectPath `
        -c Release `
        --no-build `
        --no-restore `
        --self-contained false `
        -p:PublishSingleFile=true `
        -o $releaseDir

    $outputExe = Join-Path $releaseDir "ProgramHider.exe"
    if (-not (Test-Path $outputExe)) {
        throw "Expected publish output not found at $outputExe"
    }

    Copy-Item (Join-Path $repoRoot "README.md") (Join-Path $releaseDir "README.md")
    Copy-Item (Join-Path $repoRoot "docs\CHANGELOG.md") (Join-Path $releaseDir "CHANGELOG.md")
    Copy-Item (Join-Path $repoRoot "docs\window-compatibility.md") (Join-Path $releaseDir "window-compatibility.md")

    if (-not $SkipSigning -and (Test-Path $signScript)) {
        & powershell -ExecutionPolicy Bypass -File $signScript -ExePath $outputExe
    }

    if (-not $SkipStartupSmoke) {
        & powershell -ExecutionPolicy Bypass -File $startupSmokeScript -ExePath $outputExe
    }

    Compress-Archive -Path (Join-Path $releaseDir "*") -DestinationPath $portableZip -Force

    Write-Host "Packaged executable: $outputExe"
    Write-Host "Portable zip: $portableZip"
}
finally {
    Pop-Location
}
