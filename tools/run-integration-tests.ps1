$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$backendProject = Join-Path $root 'WillyShop\WillyShop.csproj'
$frontendDirectory = Join-Path $root 'frontend'
$testProject = Join-Path $root 'tools\IntegrationSmokeTests\IntegrationSmokeTests.csproj'
$backendLog = Join-Path $root 'integration-test-backend.log'
$backendErrorLog = Join-Path $root 'integration-test-backend.err.log'
$frontendLog = Join-Path $root 'integration-test-frontend.log'
$frontendErrorLog = Join-Path $root 'integration-test-frontend.err.log'
$previousJwtSigningKey = $env:Jwt__SigningKey
$previousProxyTarget = $env:VITE_DEV_PROXY_TARGET
$env:Jwt__SigningKey = 'integration-only-willy-shop-jwt-signing-key-2026'
$env:VITE_DEV_PROXY_TARGET = 'http://localhost:5151'

$backend = $null
$frontend = $null

function Wait-ForUrl([string]$url, [int]$attempts = 180) {
    for ($attempt = 0; $attempt -lt $attempts; $attempt++) {
        Start-Sleep -Milliseconds 500
        try {
            $response = Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -eq 200) { return }
        }
        catch { }
    }
    throw "服務啟動逾時：$url"
}

try {
    Write-Host 'Building ASP.NET Core...'
    dotnet build $backendProject --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'ASP.NET Core build failed.' }

    Write-Host 'Building Vue...'
    & cmd.exe /d /c "cd /d `"$frontendDirectory`" && npm run type-check && npm run build"
    if ($LASTEXITCODE -ne 0) { throw 'Vue build failed.' }

    Write-Host 'Building integration tests...'
    dotnet build $testProject --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Integration test build failed.' }

    Remove-Item $backendLog, $backendErrorLog, $frontendLog, $frontendErrorLog -ErrorAction SilentlyContinue
    $backend = Start-Process dotnet -ArgumentList @('run', '--project', $backendProject, '--launch-profile', 'http', '--no-build') `
        -PassThru -RedirectStandardOutput $backendLog -RedirectStandardError $backendErrorLog
    Wait-ForUrl 'http://localhost:5151/api/v1/products'

    $frontend = Start-Process cmd.exe -ArgumentList @('/d', '/c', 'npm run dev -- --host localhost --port 5174') `
        -WorkingDirectory $frontendDirectory -PassThru -RedirectStandardOutput $frontendLog -RedirectStandardError $frontendErrorLog
    Wait-ForUrl 'http://localhost:5174/api/v1/products'

    $env:SMOKE_BASE_URL = 'http://localhost:5174'
    dotnet run --project $testProject --no-build
    if ($LASTEXITCODE -ne 0) { throw "Integration tests failed with exit code $LASTEXITCODE." }
}
finally {
    Remove-Item Env:SMOKE_BASE_URL -ErrorAction SilentlyContinue
    if ($null -eq $previousJwtSigningKey) {
        Remove-Item Env:Jwt__SigningKey -ErrorAction SilentlyContinue
    }
    else {
        $env:Jwt__SigningKey = $previousJwtSigningKey
    }
    if ($null -eq $previousProxyTarget) {
        Remove-Item Env:VITE_DEV_PROXY_TARGET -ErrorAction SilentlyContinue
    }
    else {
        $env:VITE_DEV_PROXY_TARGET = $previousProxyTarget
    }
    if ($frontend -and -not $frontend.HasExited) {
        & taskkill.exe /PID $frontend.Id /T /F | Out-Null
    }
    if ($backend -and -not $backend.HasExited) {
        Stop-Process -Id $backend.Id -Force -ErrorAction SilentlyContinue
    }
    Get-Process WillyShop -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}