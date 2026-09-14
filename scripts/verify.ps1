$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build FinancialPortfolioAPI.sln -c Release -m:1 -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project tests/FinancialPortfolioAPI.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet publish src/FinancialPortfolioAPI.API -c Release --no-build -o artifacts/publish
exit $LASTEXITCODE
