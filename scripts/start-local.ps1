param([int]$Port = 5080)
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
$env:Database__Provider = 'Sqlite'
$env:Database__AutoMigrate = 'true'
$env:ConnectionStrings__DefaultConnection = 'Data Source=' + (Join-Path (Get-Location) '.local/portfolio.db') + ';Default Timeout=30'
Write-Host "Portfolio tester: http://localhost:$Port/"
Write-Host "API key is stored locally in $keyPath (ignored by Git)."
dotnet run --project src/FinancialPortfolioAPI.API --no-launch-profile --urls "http://localhost:$Port"
exit $LASTEXITCODE
