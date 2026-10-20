$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$backendProject = Join-Path $root 'WillyShop\WillyShop.csproj'
$frontendDirectory = Join-Path $root 'frontend'
$testProject = Join-Path $root 'tools\IntegrationSmokeTests\IntegrationSmokeTests.csproj'
$backendLog = Join-Path $root 'integration-test-backend.log'
$backendErrorLog = Join-Path $root 'integration-test-backend.err.log'
$frontendLog = Join-Path $root 'integration-test-frontend.log'
$frontendErrorLog = Join-Path $root 'integration-test-frontend.err.log'
$adminPassword = $env:SMOKE_ADMIN_PASSWORD
$systemPassword = $env:SMOKE_SYSTEM_PASSWORD

if ([string]::IsNullOrWhiteSpace($adminPassword)) {
    throw 'SMOKE_ADMIN_PASSWORD must be set to the development Admin account password before running integration tests.'
}
if ([string]::IsNullOrWhiteSpace($systemPassword)) {
    throw 'SMOKE_SYSTEM_PASSWORD must be set to the development System account password before running integration tests.'
}

$previousSeedAdminPassword = $env:SeedUsers__AdminPassword
$previousSeedSystemPassword = $env:SeedUsers__SystemPassword
$env:SeedUsers__AdminPassword = $adminPassword
$env:SeedUsers__SystemPassword = $systemPassword

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

    $frontend = Start-Process cmd.exe -ArgumentList @('/d', '/c', 'npm run dev -- --host localhost') `
        -WorkingDirectory $frontendDirectory -PassThru -RedirectStandardOutput $frontendLog -RedirectStandardError $frontendErrorLog
    Wait-ForUrl 'http://localhost:5173/api/v1/products'

    $env:SMOKE_BASE_URL = 'http://localhost:5173'
    dotnet run --project $testProject --no-build
    if ($LASTEXITCODE -ne 0) { throw "Integration tests failed with exit code $LASTEXITCODE." }
}
finally {
    Remove-Item Env:SMOKE_BASE_URL -ErrorAction SilentlyContinue
    if ($null -eq $previousSeedAdminPassword) {
        Remove-Item Env:SeedUsers__AdminPassword -ErrorAction SilentlyContinue
    }
    else {
        $env:SeedUsers__AdminPassword = $previousSeedAdminPassword
    }
    if ($null -eq $previousSeedSystemPassword) {
        Remove-Item Env:SeedUsers__SystemPassword -ErrorAction SilentlyContinue
    }
    else {
        $env:SeedUsers__SystemPassword = $previousSeedSystemPassword
    }
    if ($frontend -and -not $frontend.HasExited) {
        & taskkill.exe /PID $frontend.Id /T /F | Out-Null
    }
    if ($backend -and -not $backend.HasExited) {
        Stop-Process -Id $backend.Id -Force -ErrorAction SilentlyContinue
    }
    Get-Process WillyShop -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}