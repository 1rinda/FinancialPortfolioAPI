# Financial Portfolio API

.NET 10 API with a browser portfolio tester, API explorer, SQLite/SQL Server persistence, API-key authentication, version checks, atomic transaction settlement, soft deletion, audit history and a committed-change feed.

## Start locally

Install the .NET 10 SDK, then run from the repository root:

```powershell
./scripts/start-local.ps1
```

Open http://localhost:5080/ for the portfolio tester or http://localhost:5080/api-docs for the API explorer. The script generates an API key in `.local/api-key.txt` and stores SQLite data in `.local/portfolio.db`; both are ignored by Git. Paste the key into the tester. The key remains in page memory only. Stop with Ctrl+C. Restarting preserves your data and key.

Dependencies restore automatically. EF Core migrations create the database on startup. No separate database installation is needed for SQLite.

## Verify

```powershell
./scripts/verify.ps1
```

This restores the EF tool, builds Release, runs executable regression and real HTTP integration checks, and publishes to `artifacts/publish`. GitHub Actions runs verification on Windows and Linux and builds the Linux container.

[Integration and deployment guide](docs/api-documentation.md) | [Operations and architecture](docs/operations.md) | [Validation record](docs/validation.md) | [OpenAPI](src/FinancialPortfolioAPI.API/docs/openapi.json) | [Postman collection](src/FinancialPortfolioAPI.API/docs/postman.json)

## Docker

Set `PORTFOLIO_API_KEY` to a private random secret of at least 32 characters, then run `docker compose up -d --build`. Open http://localhost:8080/. SQLite data persists in the `portfolio-data` volume.

This is a demonstration/test-server application using USD and fixed quotes. It does not execute real trades. The shared key grants access to every portfolio. See the guides for deployment boundaries, SQL Server setup, backups and accounting limitations.
