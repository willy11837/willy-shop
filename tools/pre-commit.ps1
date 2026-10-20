[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = Split-Path -Parent $PSScriptRoot
$frontendDirectory = Join-Path $root 'frontend'
$frontendManifest = Join-Path $frontendDirectory 'package.json'
$frontendModules = Join-Path $frontendDirectory 'node_modules'
$backendProject = Join-Path $root 'WillyShop\WillyShop.csproj'

function Assert-CommandAvailable([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Invoke-CheckedCommand(
    [string]$Label,
    [string]$FilePath,
    [string[]]$Arguments,
    [string]$WorkingDirectory
) {
    Write-Host "`n==> $Label" -ForegroundColor Cyan
    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$Label failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

try {
    Write-Host 'Running Willy Shop pre-commit guardrails...' -ForegroundColor Cyan

    if (-not (Test-Path -LiteralPath $frontendManifest -PathType Leaf)) {
        throw "Frontend manifest was not found: $frontendManifest"
    }
    if (-not (Test-Path -LiteralPath $backendProject -PathType Leaf)) {
        throw "Backend project was not found: $backendProject"
    }

    Assert-CommandAvailable 'npm.cmd'
    Assert-CommandAvailable 'dotnet'

    if (-not (Test-Path -LiteralPath $frontendModules -PathType Container)) {
        throw "Frontend dependencies are missing. Run 'npm ci' in '$frontendDirectory' before committing."
    }

    Invoke-CheckedCommand `
        -Label 'Frontend type-check and production build' `
        -FilePath 'npm.cmd' `
        -Arguments @('run', 'build') `
        -WorkingDirectory $frontendDirectory

    Invoke-CheckedCommand `
        -Label 'Backend Release build' `
        -FilePath 'dotnet' `
        -Arguments @('build', $backendProject, '--configuration', 'Release', '--nologo') `
        -WorkingDirectory $root

    Write-Host "`nPre-commit guardrails passed." -ForegroundColor Green
}
catch {
    Write-Error "Pre-commit guardrails failed: $($_.Exception.Message)"
    exit 1
}