param([ValidateRange(1, 65535)][int]$Port = 5080)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
New-Item -ItemType Directory -Force .local | Out-Null
$keyPath = Join-Path (Get-Location) '.local/api-key.txt'
if (-not (Test-Path -LiteralPath $keyPath)) {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    [IO.File]::WriteAllText($keyPath, ([BitConverter]::ToString($bytes)).Replace('-', ''))
}
$env:ApiKey = [IO.File]::ReadAllText($keyPath).Trim()
$baseUrl = "http://localhost:$Port"
$probe = [Net.Sockets.TcpClient]::new()
try {
    $connection = $probe.ConnectAsync('localhost', $Port)
    try { $occupied = $connection.Wait(1000) -and $probe.Connected }
    catch { $occupied = $false }
} finally { $probe.Dispose() }
if ($occupied) {
    try {
        $spec = Invoke-RestMethod "$baseUrl/openapi.json" -TimeoutSec 5
        if ($spec.info.title -ne 'Financial Portfolio API') { throw 'Different application.' }
        $health = Invoke-RestMethod "$baseUrl/health" -TimeoutSec 5
        if ($health.status -ne 'healthy') { throw 'API is not healthy.' }
        Invoke-RestMethod "$baseUrl/api/v1/prices" -Headers @{ 'X-Api-Key' = $env:ApiKey } -TimeoutSec 5 | Out-Null
    } catch {
        throw "Port $Port is occupied, but the portfolio API could not be verified with the local key. Stop the existing listener or check its configuration before starting again. Details: $($_.Exception.Message)"
    }
    Write-Host 'The portfolio API is already running and healthy. No rebuild is needed to use it.'
    Write-Host "API explorer: $baseUrl/api-docs"
    Write-Host "API key: $keyPath (ignored by Git)."
    Write-Host 'To rebuild code changes, stop the existing API process first, then run this script again.'
    exit 0
}
$env:Database__Provider = 'Sqlite'
$env:Database__AutoMigrate = 'true'
$env:ConnectionStrings__DefaultConnection = 'Data Source=' + (Join-Path (Get-Location) '.local/portfolio.db') + ';Default Timeout=30'
Write-Host "Portfolio tester: http://localhost:$Port/"
Write-Host "API key is stored locally in $keyPath (ignored by Git)."
dotnet run --project src/FinancialPortfolioAPI.API --no-launch-profile --urls "http://localhost:$Port"
exit $LASTEXITCODE
