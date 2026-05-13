param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('status', 'log3', 'add-all', 'commit')]
    [string]$Action,

    [string]$Message
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path -LiteralPath (Join-Path $repoRoot '.git'))) {
    throw "Git directory not found at $repoRoot"
}

switch ($Action) {
    'status' {
        & git -C $repoRoot status --short --branch
        break
    }
    'log3' {
        & git -C $repoRoot log --oneline -n 3
        break
    }
    'add-all' {
        & git -C $repoRoot add -A
        break
    }
    'commit' {
        if ([string]::IsNullOrWhiteSpace($Message)) {
            throw 'Commit message is required for Action=commit.'
        }

        & git -C $repoRoot commit -m $Message
        break
    }
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
