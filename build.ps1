param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot "app\ProgramHider\ProgramHider.csproj"
$dotnet = "D:\tooling\dotnet\dotnet.exe"

if (-not (Test-Path $dotnet)) {
    throw "Expected .NET SDK host not found at $dotnet"
}

$versionLine = Select-String -Path $projectPath -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
if (-not $versionLine) {
    throw "Unable to read version from $projectPath"
}

$version = $versionLine.Matches[0].Groups[1].Value
$releaseDir = Join-Path $repoRoot "release\v$version"

Push-Location $repoRoot
try {
    if (Test-Path $releaseDir) {
        Remove-Item -Recurse -Force $releaseDir
    }

    & $dotnet build $projectPath -c Release

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

    Write-Host "Packaged executable: $outputExe"
}
finally {
    Pop-Location
}
