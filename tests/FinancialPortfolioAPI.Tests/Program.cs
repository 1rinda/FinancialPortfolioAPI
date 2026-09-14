using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialPortfolioAPI.Application;
using FinancialPortfolioAPI.Domain;
using FinancialPortfolioAPI.Infrastructure;
using FinancialPortfolioAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

var directory = Path.Combine(Path.GetTempPath(), "portfolio-tests-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
var dbPath = Path.Combine(directory, "test.json");
int passed = 0;
void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); passed++; Console.WriteLine("PASS: " + label); }
void Conflict(Action action, string label) {
    try { action(); throw new Exception("Expected conflict: " + label); }
    catch (ApiException ex) when (ex.Status == 409) { Check(true, label); }
}
Guid portfolioId = Guid.Empty;
var sqliteOptions = new DbContextOptionsBuilder<SqlitePortfolioDbContext>().UseSqlite("Data Source=" + Path.Combine(directory, "regression.db")).Options;
using (var context = new SqlitePortfolioDbContext(sqliteOptions)) {
    context.Database.Migrate();
    Check(!context.Database.HasPendingModelChanges(), "SQLite migration matches model");
}
using (var context = new SqlServerPortfolioDbContext(new DbContextOptionsBuilder<SqlServerPortfolioDbContext>()
    .UseSqlServer("Server=localhost;Database=SchemaValidation;Integrated Security=True;TrustServerCertificate=True").Options)) {
    Check(!context.Database.HasPendingModelChanges(), "SQL Server migration matches model");
    var script = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().GenerateScript();
    Check(script.Contains("CREATE TABLE [Portfolios]"), "SQL Server migration generates DDL");
}
using var jsonStore = new JsonPortfolioStore(dbPath);
var stores = new List<IPortfolioStore> { jsonStore, new EfPortfolioStore(() => new SqlitePortfolioDbContext(sqliteOptions)) };
var sqlConnection = Environment.GetEnvironmentVariable("PORTFOLIO_TEST_SQLSERVER");
if (!string.IsNullOrWhiteSpace(sqlConnection)) {
    var sqlOptions = new DbContextOptionsBuilder<SqlServerPortfolioDbContext>().UseSqlServer(sqlConnection).Options;
    using var context = new SqlServerPortfolioDbContext(sqlOptions);
    context.Database.Migrate();
    Check(!context.Portfolios.IgnoreQueryFilters().Any(), "SQL Server test database starts empty");
    stores.Insert(1, new EfPortfolioStore(() => new SqlServerPortfolioDbContext(sqlOptions)));
}
foreach (IPortfolioStore store in stores)
{
    var service = new PortfolioService(store, new DemoPricingService());
    var p = service.Create(new("Test", "CL001", "Test Client", RiskProfile.Moderate));
    portfolioId = p.Id;
    var before = store.Read(db => JsonSerializer.Serialize(db));
    service.Get(p.Id);
    Check(before == store.Read(db => JsonSerializer.Serialize(db)), "GET does not mutate storage");
    var depositRequest = new CreateTransactionRequest(TransactionType.Deposit, "deposit-1", Amount: 10000);
    var deposit = service.CreateTransaction(p.Id, depositRequest);
    Check(service.CreateTransaction(p.Id, depositRequest).Id == deposit.Id, "transaction retry returns original");
    Conflict(() => service.CreateTransaction(p.Id, depositRequest with { Amount = 20000 }), "reference payload mismatch rejected");
    deposit = service.SetStatus(deposit.Id, new(deposit.Version, TransactionStatus.Executed));
    Check(service.Get(p.Id).CashBalance == 0, "execution does not post cash");
    deposit = service.SetStatus(deposit.Id, new(deposit.Version, TransactionStatus.Settled));
    Check(service.Get(p.Id).CashBalance == 10000, "deposit posts on settlement");
    Conflict(() => service.SetStatus(deposit.Id, new(deposit.Version, TransactionStatus.Settled)), "double settlement rejected");
    var buy = service.CreateTransaction(p.Id, new(TransactionType.Buy, "buy-1", "AAPL", 10, 170, Commission: 5));
    buy = service.SetStatus(buy.Id, new(buy.Version, TransactionStatus.Executed));
    buy = service.SetStatus(buy.Id, new(buy.Version, TransactionStatus.Settled));
    p = service.Get(p.Id);
    Check(p.CashBalance == 8295 && p.TotalValue == 10080 && p.UnrealizedGainLoss == 80, "buy accounting and valuation");
    Check(p.Holdings.Single().CostBasis == 170.5m && p.Allocation.Values.Sum() == 100, "commission basis and cash allocation");
    var excessive = service.CreateTransaction(p.Id, new(TransactionType.Withdrawal, "withdraw-1", Amount: 20000));
    excessive = service.SetStatus(excessive.Id, new(excessive.Version, TransactionStatus.Executed));
    before = store.Read(db => JsonSerializer.Serialize(db));
    Conflict(() => service.SetStatus(excessive.Id, new(excessive.Version, TransactionStatus.Settled)), "insufficient funds rejected");
    Check(before == store.Read(db => JsonSerializer.Serialize(db)), "failed settlement rolls back balances and change log");
    var oversell = service.CreateTransaction(p.Id, new(TransactionType.Sell, "oversell", "AAPL", 11, 180));
    oversell = service.SetStatus(oversell.Id, new(oversell.Version, TransactionStatus.Executed));
    Conflict(() => service.SetStatus(oversell.Id, new(oversell.Version, TransactionStatus.Settled)), "short sale rejected");
    var sell = service.CreateTransaction(p.Id, new(TransactionType.Sell, "sell", "AAPL", 10, 180, Commission: 5));
    sell = service.SetStatus(sell.Id, new(sell.Version, TransactionStatus.Executed));
    service.SetStatus(sell.Id, new(sell.Version, TransactionStatus.Settled));
    p = service.Get(p.Id);
    Check(p.Holdings.Count == 0 && p.CashBalance == 10090, "sell removes position and credits net proceeds");
    var version = p.Version;
    int won = 0, lost = 0;
    Parallel.For(0, 8, i => {
        try { service.Update(p.Id, new(version, "Update " + i, RiskProfile.Moderate, PortfolioStatus.Active)); Interlocked.Increment(ref won); }
        catch (ApiException ex) when (ex.Status == 409) { Interlocked.Increment(ref lost); }
    });
    Check(won == 1 && lost == 7, "concurrent stale updates cannot overwrite");
    var changes = store.Read(db => db.Changes.ToList());
    Check(changes.Select(c => c.Sequence).SequenceEqual(Enumerable.Range(1, changes.Count).Select(i => (long)i)), "committed change sequence is contiguous");
    try { using var second = new JsonPortfolioStore(dbPath); throw new Exception("Second store should fail"); }
    catch (IOException) { Check(true, "second process/store lock rejected"); }
}
using (var reopened = new SqlitePortfolioDbContext(sqliteOptions)) {
    Check(reopened.Portfolios.Single(p => p.Id == portfolioId).CashBalance == 10090, "SQLite data survives reopening");
    Check(!reopened.Holdings.Any(), "SQLite sold holdings removed from database");
    Check(reopened.AuditLogs.Count() == reopened.Changes.Count(), "audit and changes committed together");
    Check(reopened.PriceHistories.Any(), "demo price history persisted");
}
var efStore = new EfPortfolioStore(() => new SqlitePortfolioDbContext(sqliteOptions));
var efService = new PortfolioService(efStore, new DemoPricingService());
var empty = efService.Create(new("Delete test", "C", "Client", RiskProfile.Moderate));
efService.Delete(empty.Id, empty.Version);
try { efService.Get(empty.Id); throw new Exception("Deleted portfolio must be hidden"); }
catch (ApiException ex) when (ex.Status == 404) { Check(true, "deleted portfolio returns 404"); }
using (var context = new SqlitePortfolioDbContext(sqliteOptions)) {
    Check(context.Portfolios.IgnoreQueryFilters().Single(p => p.Id == empty.Id).IsDeleted, "soft deletion retains database row");
    Check(context.AuditLogs.Any(a => a.EntityId == empty.Id && a.Action == "Deleted"), "soft deletion audited");
}


// Exercise the real HTTP pipeline, including validation, auth and documentation.
var root = Directory.GetCurrentDirectory();
var apiDirectory = Path.Combine(root, "src", "FinancialPortfolioAPI.API");
var dll = Path.Combine(apiDirectory, "bin", "Release", "net10.0", "FinancialPortfolioAPI.API.dll");
if (!File.Exists(dll)) throw new Exception("Build the solution in Release and run tests from repository root.");
var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
listener.Start(); var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
var secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
var start = new ProcessStartInfo("dotnet") { WorkingDirectory = apiDirectory, UseShellExecute = false, CreateNoWindow = true };
start.ArgumentList.Add(dll); start.ArgumentList.Add("--urls"); start.ArgumentList.Add("http://127.0.0.1:" + port);
start.Environment["ApiKey"] = secret;
start.Environment["Database__Provider"] = "Sqlite";
start.Environment["Database__AutoMigrate"] = "true";
start.Environment["ConnectionStrings__DefaultConnection"] = "Data Source=" + Path.Combine(directory, "http.db");
start.Environment["Logging__LogLevel__Default"] = "Warning";
using var process = Process.Start(start)!;
try {
    using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(5) };
    bool ready = false;
    for (var i = 0; i < 60; i++) {
        if (process.HasExited) throw new Exception("API exited during startup");
        try { ready = (await client.GetAsync("/health")).IsSuccessStatusCode; if (ready) break; } catch (HttpRequestException) { }
        await Task.Delay(250);
    }
    Check(ready, "HTTP health");
    Check((await client.GetAsync("/api/v1/portfolio")).StatusCode == HttpStatusCode.Unauthorized, "HTTP rejects missing key");
    client.DefaultRequestHeaders.Add("X-Api-Key", secret);
    Check((await client.GetAsync("/api/v1/portfolio?limit=101")).StatusCode == HttpStatusCode.BadRequest, "HTTP pagination validation");
    Check((await client.PostAsJsonAsync("/api/v1/portfolio", new { name = " ", clientId = "C", clientName = "Test" })).StatusCode == HttpStatusCode.BadRequest, "HTTP required text validation");
    Check((await client.PostAsJsonAsync("/api/v1/portfolio", new { name = "Test", clientId = "C", clientName = "Test", riskProfile = "Unknown" })).StatusCode == HttpStatusCode.BadRequest, "HTTP enum validation");
    var created = await client.PostAsJsonAsync("/api/v1/portfolio", new { name = "HTTP Test", clientId = "C", clientName = "Test", riskProfile = "Moderate" });
    Check(created.StatusCode == HttpStatusCode.Created && created.Headers.Location != null, "HTTP creates resource with Location");
    var resource = await created.Content.ReadFromJsonAsync<JsonElement>();
    var id = resource.GetProperty("id").GetString();
    var invalid = await client.PostAsJsonAsync($"/api/v1/transaction/portfolio/{id}", new { type = "Deposit", referenceNumber = "negative", amount = -5 });
    Check(invalid.StatusCode == HttpStatusCode.BadRequest, "HTTP rejects negative amount");
    var missingType = await client.PostAsJsonAsync($"/api/v1/transaction/portfolio/{id}", new { referenceNumber = "missing-type", symbol = "AAPL", shares = 1, price = 10 });
    Check(missingType.StatusCode == HttpStatusCode.BadRequest, "HTTP requires explicit transaction type");
    var stale = await client.PutAsJsonAsync($"/api/v1/portfolio/{id}", new { expectedVersion = 5, name = "Changed", riskProfile = "Moderate", status = "Active" });
    Check(stale.StatusCode == HttpStatusCode.Conflict && stale.Content.Headers.ContentType?.MediaType == "application/problem+json", "HTTP stale update returns ProblemDetails");
    Check((await client.GetAsync("/api/v1/portfolio/" + Guid.NewGuid())).StatusCode == HttpStatusCode.NotFound, "HTTP missing resource");
    Check((await client.GetAsync("/docs")).IsSuccessStatusCode && (await client.GetAsync("/docs/guide")).IsSuccessStatusCode, "HTTP documentation available");
    var spec = await client.GetFromJsonAsync<JsonElement>("/openapi.json");
    Check(spec.GetProperty("paths").EnumerateObject().Count() == 10 && spec.GetProperty("paths").GetProperty("/api/v1/portfolio/{id}").TryGetProperty("delete", out _), "OpenAPI contains all API paths");
    foreach (var path in new[] { "/", "/api-docs", "/postman.json", "/js/tester.js", "/js/explorer.js", "/css/tester.css", "/api/v1/audit", "/api/v1/prices/AAPL/history" })
        Check((await client.GetAsync(path)).IsSuccessStatusCode, "HTTP available: " + path);
    var feed = await client.GetFromJsonAsync<JsonElement>("/api/v1/changes?after=0");
    var cursor = feed.GetProperty("nextCursor").GetInt64();
    var next = await client.GetFromJsonAsync<JsonElement>("/api/v1/changes?after=" + cursor);
    Check(cursor == 1 && next.GetProperty("items").GetArrayLength() == 0, "HTTP change cursor resumes without duplicates");
}
finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
Console.WriteLine($"All {passed} checks passed. Temporary test data: {directory}");
