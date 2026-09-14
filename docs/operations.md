# Operations and architecture

## Configuration

Environment variables override appsettings. Never commit real secrets or connection strings containing passwords.

| Variable | Default | Purpose |
| --- | --- | --- |
| `ApiKey` | Required, at least 32 characters | Shared API credential |
| `Database__Provider` | `Sqlite` | `Sqlite` or `SqlServer` |
| `ConnectionStrings__DefaultConnection` | SQLite under API content root `data/portfolio.db` | Provider connection string |
| `Database__AutoMigrate` | `true` | Apply checked-in migrations at startup |
| `ASPNETCORE_URLS` | Hosting default | Listener URLs; local script uses loopback port 5080 |

The local script sets explicit SQLite configuration and uses ignored `.local/` storage. It stores the generated development secret in plaintext; protect that folder using your account's filesystem permissions. Use a deployment secret store/environment injection on a server. The Docker image uses `/data/portfolio.db`, runs as `app`, and persists that directory using a named volume. Create the parent directory yourself when providing another SQLite path.

## SQL Server

Use a dedicated database and an identity with permission to create/apply its schema. For example, on a Windows development machine with SQL Server and integrated authentication:

```powershell
$env:ApiKey = (Get-Content .local/api-key.txt -Raw).Trim()
$env:Database__Provider = 'SqlServer'
$env:ConnectionStrings__DefaultConnection = 'Server=localhost;Database=FinancialPortfolioDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet run --project src/FinancialPortfolioAPI.API --no-launch-profile --urls http://localhost:5080
```

`TrustServerCertificate=True` is for a local development certificate. For remote deployment use a trusted server certificate and `TrustServerCertificate=False`. Changing provider creates/selects a different database; it does not transfer existing data.

## Migrations

The local EF tool is pinned in `dotnet-tools.json`. Run from the repository root:

```powershell
dotnet tool restore
dotnet ef database update --project src/FinancialPortfolioAPI.Infrastructure --context SqlitePortfolioDbContext
```

Set `ConnectionStrings__DefaultConnection` to the intended database first. The design-time SQLite fallback is `portfolio.db` in the working directory, whereas runtime fallback is under the API content root. Use an explicit connection string to avoid migrating the wrong database.

For SQL Server use `--context SqlServerPortfolioDbContext`. To review SQL before applying:

```powershell
dotnet ef migrations script --project src/FinancialPortfolioAPI.Infrastructure --context SqlServerPortfolioDbContext --idempotent --output artifacts/sqlserver.sql
```

Create `artifacts` first if absent. Apply migrations through a deployment identity, then set `Database__AutoMigrate=false` for a runtime identity without schema privileges. Startup refuses an unavailable database or pending migrations. For future model changes, generate and commit a migration for **each** context in its `Data/Migrations/Sqlite` or `Data/Migrations/SqlServer` directory and verify both model snapshots.

## Backup and recovery

For SQLite, stop all API processes before copying the database and any associated journal/WAL files as one backup. For Docker, stop the Compose service and back up the named volume using your host's volume backup tooling. Restart after copying. Do not remove the volume with `docker compose down -v` unless intentionally discarding its data.

For SQL Server use database-native backups and test restore procedures. Retain the migration history table alongside application tables. Before restoring, stop API writers; restore the backup, restart with a compatible application version, verify `/health`, then check representative portfolios. Consumers must reset their change-feed cursors and resynchronize after restoring an older backup.

## Architecture and boundaries

Controllers validate transport inputs and call the Application service. Domain models calculate values using decimal arithmetic. `EfPortfolioStore` wraps each operation in a serializable transaction; writes update the `StoreStates` row before reading balances/versions, preventing competing writers from checking stale state. A single commit includes balance/holding changes, transaction status, feed events, audit snapshots and quote observations. Terminal transaction transitions and version checks prevent duplicate settlement.

There is no external trade execution, tenant authorization, live pricing, realized-gain reporting or tax accounting. The API key identifies a shared integration; audit actor `shared-api-key` does not identify an individual person. Remote IP is the direct peer, potentially a reverse proxy. Before remote use configure HTTPS, intended network access, and appropriate secret handling. Database/audit contents include client information.

The browser tester is at `/`, the interactive contract explorer at `/api-docs`, and downloadable contracts at `/openapi.json` and `/postman.json`. In Postman set `baseUrl` and `apiKey`; copy returned IDs into `id`/`portfolioId` and update `expectedVersion` before modifying resources. The collection never includes a credential.

## Verification and troubleshooting

`scripts/verify.ps1` builds, runs the executable test harness and publishes. The harness uses isolated temporary SQLite/JSON files and a child API on a free loopback port, stopping that API afterward. It tests accounting, transaction retries, failed-settlement rollback, concurrent version conflicts, persistence, audit/deletion, authentication, validation, documentation and change polling. This is an executable harness: `dotnet test` does not run it.

To include SQL Server integration, set `PORTFOLIO_TEST_SQLSERVER` to a dedicated **empty test database** connection string before verification. Migrations and fixtures are written to that database; the harness deliberately retains it for inspection. Use a fresh database name for each run. Without that variable, SQL Server verification checks the migration snapshot and generated DDL only.

Startup key error: use the local script or supply a sufficiently long `ApiKey`. Database startup error: check provider, connection string, parent-directory permissions and migrations. HTTP 401: paste the key for the running process. HTTP 409: retrieve the current version and check funds, shares and transaction status. Docker connection error: start Docker Desktop/Engine and confirm `docker info`. All unexpected request errors are logged with a trace ID; clients receive generic problem details.

CI runs on Windows and Linux; Linux additionally builds the container. Server provisioning, DNS and public HTTPS deployment require an actual deployment target and are outside the local setup.

## Optional browser check

With the local API running on port 5080 and Microsoft Edge installed:

```powershell
npm install --prefix .local/browser --no-audit --no-fund playwright
node scripts/browser-smoke.cjs
```

This opens a headless browser, connects, creates/edits/soft-deletes a test portfolio, checks the mobile layout and sends an API explorer request. Test audit/change records remain in the local database. Screenshots and browser dependencies stay in ignored `.local/`. The browser check is optional and is not part of CI.

For the background development process started during setup, logs are in `.local/server.log` and `.local/server-error.log`; `.local/server.pid` identifies its PowerShell parent. Stop that process tree before starting another instance on the same port. Normal use of `start-local.ps1` runs in your terminal and stops with Ctrl+C.
