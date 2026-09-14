using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using FinancialPortfolioAPI.Application;
using FinancialPortfolioAPI.Infrastructure;
using FinancialPortfolioAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var apiKey = builder.Configuration["ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < 32)
    throw new InvalidOperationException("Set ApiKey (at least 32 characters) using environment variables or user secrets.");
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)) {
    if (string.IsNullOrWhiteSpace(connection)) {
        var dataPath = Path.Combine(builder.Environment.ContentRootPath, "data");
        Directory.CreateDirectory(dataPath);
        connection = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(dataPath, "portfolio.db"), DefaultTimeout = 30 }.ToString();
    }
    builder.Services.AddDbContextFactory<SqlitePortfolioDbContext>(o => o.UseSqlite(connection));
    builder.Services.AddSingleton<Func<PortfolioDbContext>>(sp => () => sp.GetRequiredService<IDbContextFactory<SqlitePortfolioDbContext>>().CreateDbContext());
} else if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)) {
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("SQL Server requires ConnectionStrings__DefaultConnection.");
    builder.Services.AddDbContextFactory<SqlServerPortfolioDbContext>(o => o.UseSqlServer(connection));
    builder.Services.AddSingleton<Func<PortfolioDbContext>>(sp => () => sp.GetRequiredService<IDbContextFactory<SqlServerPortfolioDbContext>>().CreateDbContext());
} else throw new InvalidOperationException("Database:Provider must be Sqlite or SqlServer.");
builder.Services.AddSingleton<IPortfolioStore>(sp => new EfPortfolioStore(sp.GetRequiredService<Func<PortfolioDbContext>>(), () => {
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    return (http == null ? "system" : "shared-api-key", http?.Connection.RemoteIpAddress?.ToString());
}));
builder.Services.AddSingleton<IPricingService, DemoPricingService>();
builder.Services.AddSingleton<PortfolioService>();
var app = builder.Build();
using (var db = app.Services.GetRequiredService<Func<PortfolioDbContext>>()()) {
    if (builder.Configuration.GetValue("Database:AutoMigrate", true)) db.Database.Migrate();
    if (!db.Database.CanConnect() || db.Database.GetPendingMigrations().Any())
        throw new InvalidOperationException("Database is unavailable or migrations are pending.");
}
app.Use(async (context, next) => {
    try { await next(context); }
    catch (ApiException ex) { await Results.Problem(statusCode: ex.Status, title: ex.Message).ExecuteAsync(context); }
    catch (Exception ex) {
        app.Logger.LogError(ex, "Request failed: {TraceId}", context.TraceIdentifier);
        await Results.Problem(statusCode: 500, title: "An unexpected error occurred.", extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
    }
});
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api")) {
        var supplied = context.Request.Headers["X-Api-Key"].ToString();
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)))) {
            await Results.Problem(statusCode: 401, title: "A valid X-Api-Key header is required.").ExecuteAsync(context); return;
        }
        context.Response.Headers.CacheControl = "no-store";
    }
    await next(context);
});
app.UseStaticFiles();
app.MapGet("/health", (Func<PortfolioDbContext> factory) => {
    try { using var db = factory(); return db.Database.CanConnect() && db.StoreStates.Any(s => s.Id == 1)
        ? Results.Ok(new { status = "healthy", database = provider }) : Results.StatusCode(503); }
    catch { return Results.StatusCode(503); }
});
app.MapGet("/openapi.json", () => Results.File(Path.Combine(app.Environment.ContentRootPath, "docs", "openapi.json"), "application/json"));
app.MapGet("/postman.json", () => Results.File(Path.Combine(app.Environment.ContentRootPath, "docs", "postman.json"), "application/json"));
app.MapRazorPages();
app.MapGet("/docs", () => Results.Content("""
<!doctype html><html lang="en"><meta charset="utf-8"><title>Financial Portfolio API</title>
<style>body{font:17px system-ui;max-width:850px;margin:60px auto;padding:20px;line-height:1.6}a{color:#1255aa}code{background:#eee;padding:3px}</style>
<h1>Financial Portfolio API v1</h1><p>A test-server integration API for portfolios and financial transactions.</p>
<p><a href="/openapi.json">Download OpenAPI 3.0 specification</a> — import this into Postman, Swagger Editor or your client generator.</p>
<p><a href="/docs/guide">Read the integration and deployment guide</a></p>
<p>Send <code>X-Api-Key</code> on every <code>/api/v1</code> request. All amounts are USD; prices are fixed demonstration values.</p>
<p>Create a portfolio, create a deposit, move it from Pending to Executed to Settled, then buy holdings. Settlement commits cash, holdings and change events atomically.</p>
<p>Poll <code>GET /api/v1/changes?after=0</code> and persist the returned cursor after processing each batch.</p></html>
""", "text/html"));
app.MapGet("/docs/guide", () => Results.File(Path.Combine(AppContext.BaseDirectory, "docs", "api-documentation.md"), "text/plain; charset=utf-8"));
app.MapControllers();
app.Run();
