# Financial Portfolio API integration guide

## Purpose and contract
A .NET 10 REST API for other systems to retrieve portfolios, submit transactions, update metadata and discover committed changes. All API routes start with /api/v1. JSON property names use camelCase and enum values use strings. Dates are UTC ISO 8601. IDs are UUIDs.

The API starts empty. All portfolios use USD. AAPL, MSFT and NVDA prices are fixed demonstration fixtures, not current market prices. Inputs allow up to six decimal places. Cost basis rounds to twelve decimal places using midpoint-to-even rounding; SQL Server stores decimals as decimal(38,12). Financial values use C# decimal without currency rounding; clients should use decimal types. Total value is cash plus shares times current demo price. Cost basis is weighted average per share including buy commission. Unrealized gain/loss excludes realized gains. No YTD, annual performance, tax, FX, live trading or historical performance is claimed.

## Authentication
Every /api route requires an X-Api-Key header. Configure ApiKey with a secret of at least 32 characters. No default secret is included. The key grants access to every portfolio; clientId is a filter, not an authorization boundary. Share credentials privately. /health, /, /api-docs, /docs, /docs/guide, /openapi.json, /postman.json and static assets are public. The tester and explorer keep credentials in page memory; reload or disconnect to clear them.

## Endpoints
| Method | Path | Purpose |
| --- | --- | --- |
| GET | /api/v1/portfolio | List portfolios; optional clientId, status, riskProfile, offset and limit |
| POST | /api/v1/portfolio | Create empty portfolio |
| GET | /api/v1/portfolio/{id} | Get balances, holdings, allocation and version |
| PUT | /api/v1/portfolio/{id} | Replace name, riskProfile and status using expectedVersion |
| DELETE | /api/v1/portfolio/{id}?expectedVersion=1 | Soft-delete an empty portfolio, retaining history |
| GET | /api/v1/audit | Paginated audit records; optional entityId |
| GET | /api/v1/prices/{symbol}/history | Paginated recorded demo quote observations |
| POST | /api/v1/portfolio/{id}/recalculate | Refresh demo prices using expectedVersion |
| GET | /api/v1/transaction/portfolio/{portfolioId} | List portfolio transactions |
| POST | /api/v1/transaction/portfolio/{portfolioId} | Create pending transaction |
| GET | /api/v1/transaction/{id} | Get transaction and version |
| PATCH | /api/v1/transaction/{id}/status | Advance status using expectedVersion |
| GET | /api/v1/changes | Poll committed changes using after and limit |
| GET | /api/v1/prices | Supported symbols and fixed prices |

Lists return {total, offset, limit, items}. offset defaults to 0; limit defaults to 50, maximum 100. The change feed returns {items, nextCursor}. GET never modifies data. Creation returns 201 and a Location header. Other successful API requests return 200.

Risk profiles: Conservative, Moderate, Aggressive, VeryAggressive.
Portfolio statuses: Active, Inactive, PendingReview, Restricted, Closed.
Closed portfolios are immutable and must have no cash, holdings or outstanding transactions before closure.

## Working PowerShell example
Start the service as described in README.md. Run these requests from the repository root in another terminal:

```powershell
$env:ApiKey = (Get-Content .local/api-key.txt -Raw).Trim()
$base = "http://localhost:5080"
$headers = @{ "X-Api-Key" = $env:ApiKey }
function Call-Api($method, $path, $body) {
    $args = @{ Method = $method; Uri = "$base$path"; Headers = $headers }
    if ($null -ne $body) {
        $args.ContentType = "application/json"
        $args.Body = $body | ConvertTo-Json -Depth 10
    }
    Invoke-RestMethod @args
}
$p = Call-Api POST "/api/v1/portfolio" @{
    name = "Technology Portfolio"; clientId = "CL001"
    clientName = "Demo Client"; riskProfile = "Moderate"
}
$t = Call-Api POST "/api/v1/transaction/portfolio/$($p.id)" @{
    type = "Deposit"; referenceNumber = "deposit-001"; amount = 10000
}
$t = Call-Api PATCH "/api/v1/transaction/$($t.id)/status" @{
    expectedVersion = $t.version; status = "Executed"
}
$t = Call-Api PATCH "/api/v1/transaction/$($t.id)/status" @{
    expectedVersion = $t.version; status = "Settled"
}
$buy = Call-Api POST "/api/v1/transaction/portfolio/$($p.id)" @{
    type = "Buy"; referenceNumber = "buy-001"; symbol = "AAPL"
    shares = 10; price = 170; commission = 5
}
$buy = Call-Api PATCH "/api/v1/transaction/$($buy.id)/status" @{
    expectedVersion = $buy.version; status = "Executed"
}
$buy = Call-Api PATCH "/api/v1/transaction/$($buy.id)/status" @{
    expectedVersion = $buy.version; status = "Settled"
}
$p = Call-Api GET "/api/v1/portfolio/$($p.id)" $null
# cashBalance = 8295; totalValue = 10080; unrealizedGainLoss = 80.
$p = Call-Api PUT "/api/v1/portfolio/$($p.id)" @{
    expectedVersion = $p.version; name = "Updated Portfolio"
    riskProfile = "Aggressive"; status = "Active"
}
$changes = Call-Api GET "/api/v1/changes?after=0&limit=100" $null
$changes.items
```

## Transaction rules and retries
Buy and Sell require a supported symbol, positive shares and price; amount must be zero. Commission is nonnegative and must not exceed sale proceeds.
Deposit, Withdrawal and Dividend require positive amount; omit symbol, shares, price and commission.
Transfer and Rebalance are intentionally unsupported because they require paired postings or multi-trade accounting.

Lifecycle:
- Pending -> Executed, Cancelled or Failed.
- Executed -> Settled or Failed.
- Settled, Cancelled and Failed are terminal.

Only settlement changes balances. It atomically checks funds/shares, changes holdings and cash, records settlement and emits changes. Execution does not reserve funds or shares: settlement may fail with 409 if other settlements consume them. Failed settlement leaves the transaction Executed and all data unchanged. No short selling or negative cash is allowed. Trades and settlements require an Active portfolio.

referenceNumber is unique within a portfolio. Retrying the same normalized reference and request returns the original transaction (201, same ID), even if its status advanced. Reusing it with a different payload returns 409. Retrying settlement with an old version returns 409; retrieve the transaction to determine whether the earlier attempt succeeded. Portfolio creation is not idempotent.

Holdings are changed through settled trades, rather than direct edits that could bypass cash accounting.

## Version checks and polling
Every portfolio and transaction has a monotonically increasing version. PUT, recalculation and status PATCH require expectedVersion. A stale version returns 409: GET the latest resource and reconcile your changes before retrying.

To synchronize:
1. Call GET /api/v1/changes?after=0&limit=100.
2. For each item with action Updated, fetch its Portfolio or Transaction by entityId. For action Deleted, remove the portfolio from your local projection; GET returns 404.
3. Persist nextCursor only after processing the batch successfully.
4. Continue from that cursor; poll again after a suitable delay when items is empty.

Sequence IDs are global and ordered. Failed operations emit no changes. A settlement emits a Portfolio and Transaction event in one commit; batches may split these events. The feed identifies changes, not historical snapshots. GET returns the current version, which may be newer than the event. Consumers should tolerate duplicates and use idempotent upserts. Soft deletion retains the database row, transactions and audit history; deleted portfolios are excluded from list/get. There is no event retention limit. If restoring an older backup, consumers must resynchronize and reset their cursors.

## Errors
Errors use application/problem+json with title and status. Validation errors may include an errors dictionary.
- 400: malformed JSON, invalid enum, invalid values or unsupported symbol.
- 401: missing or incorrect API key.
- 404: unknown resource.
- 409: stale version, duplicate reference with different payload, invalid transition, inactive portfolio or insufficient funds/shares.
- 500: unexpected server/storage error; details are logged server-side.

## Deploy to a test server
### Docker
Install Docker on the server. From the repository root, set PORTFOLIO_API_KEY to a generated secret, then:
```powershell
docker compose up -d --build
```
The API listens on port 8080 and persists data in the portfolio-data volume. Put it behind an HTTPS reverse proxy before remote use. Configure firewall access for intended consumers. Do not use docker compose down -v if you want to keep the data.

### Windows / IIS or a managed process
```powershell
dotnet publish src/FinancialPortfolioAPI.API -c Release -o publish -m:1 -p:UseSharedCompilation=false
$env:ApiKey = "<your-generated-secret-at-least-32-characters>"
$env:Database__Provider = "Sqlite"
$env:ConnectionStrings__DefaultConnection = "Data Source=C:\PortfolioData\portfolio.db;Default Timeout=30"
Set-Location publish
dotnet FinancialPortfolioAPI.API.dll --urls http://127.0.0.1:5080
```
Install the .NET 10 ASP.NET Core Runtime on the server, or the .NET 10 Hosting Bundle for IIS. Give the service identity write permission to the storage directory. Configure environment variables in the service/IIS configuration and run one worker process. Use a managed service or IIS for automatic restarts and an HTTPS binding/reverse proxy for remote access. Recycle the process when rotating the key.

Health check: GET /health. Share the server's /docs URL and /openapi.json with the consuming team. Import the OpenAPI file into Postman or a client generator and set the server URL. Credentials are shared separately.

### Storage scope
SQLite is the default; SQL Server is also supported. Provider-specific EF Core migrations create tables for portfolios, holdings, transactions, committed changes, audit logs, recorded demo prices and a serialization row. A write acquires that row inside a serializable database transaction before loading state, then commits all effects together. Failed operations roll back. See [operations.md](operations.md) for configuration, migration commands and backup procedures.

The old JsonPortfolioStore remains for regression coverage and reference only; the API does not select it and does not import old JSON data. Do not point an existing JSON file at the SQLite connection string.

The service currently loads all portfolios, transactions and changes into memory per operation and serializes writes globally. Deploy one API instance for the intended small test-server workload. Database coordination is exercised by concurrent requests but high-volume/distributed operation is not qualified. Before real customer use, add per-client authorization, live pricing and a reviewed accounting/audit design. Audit records include before/after snapshots and may contain client data; access uses the same shared API key. Price history records quote observations during buys/recalculations, not market price history.

## Project layout
- Domain: portfolio, holding, transaction and committed-change models.
- Application: request DTOs, interfaces, validation and settlement logic.
- Infrastructure: EF Core contexts, SQLite/SQL Server migrations, transactional store and fixed pricing provider.
- API: controllers, authentication, error handling, Razor tester/explorer and documentation downloads.
- tests: executable regression tests with no third-party test packages.
