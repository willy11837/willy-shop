[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Resolve-GitExecutable {
    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($git) {
        return $git.Source
    }

    $knownLocations = @(
        'C:\Program Files\Git\cmd\git.exe',
        'C:\Program Files\Git\bin\git.exe',
        'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe'
    )

    foreach ($candidate in $knownLocations) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw 'Git was not found. Install Git or add it to PATH before installing hooks.'
}

$git = Resolve-GitExecutable

if (-not (Test-Path -LiteralPath (Join-Path $root '.git') -PathType Container)) {
    throw "The repository has not been initialized: $root"
}

& $git -C $root config core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) {
    throw "Failed to configure core.hooksPath (exit code $LASTEXITCODE)."
}

$configuredPath = & $git -C $root config --get core.hooksPath
if ($LASTEXITCODE -ne 0 -or $configuredPath -ne '.githooks') {
    throw "Hook installation verification failed. Configured value: '$configuredPath'"
}

Write-Host "Git hooks enabled for $root" -ForegroundColor Green
Write-Host 'core.hooksPath = .githooks'