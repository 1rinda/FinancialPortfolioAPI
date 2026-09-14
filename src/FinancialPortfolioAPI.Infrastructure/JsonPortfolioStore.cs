using System.Text.Json;
using System.Text.Json.Serialization;
using FinancialPortfolioAPI.Application;
using FinancialPortfolioAPI.Domain;
namespace FinancialPortfolioAPI.Infrastructure;

// Single-process test-server storage. Each write commits the entire state and change feed together.
public sealed class JsonPortfolioStore : IPortfolioStore, IDisposable
{
    readonly object gate = new();
    readonly string path;
    readonly FileStream processLock;
    readonly JsonSerializerOptions options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public JsonPortfolioStore(string path)
    {
        this.path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
        processLock = new FileStream(this.path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (!File.Exists(this.path)) Save(new Database());
        _ = Load(); // Fail startup on corrupted storage instead of resetting it.
    }
    Database Load() => JsonSerializer.Deserialize<Database>(File.ReadAllText(path), options) ?? throw new InvalidDataException("Invalid database.");
    void Save(Database db)
    {
        var temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) {
            JsonSerializer.Serialize(stream, db, options); stream.Flush(true);
        }
        File.Move(temporary, path, true);
    }
    public T Read<T>(Func<Database, T> action) { lock (gate) return action(Load()); }
    public T Write<T>(Func<Database, T> action) {
        lock (gate) { var db = Load(); var result = action(db); Save(db); return result; }
    }
    public void Dispose() => processLock.Dispose();
}
public sealed class DemoPricingService : IPricingService
{
    static readonly Quote[] Quotes = [new("AAPL", "Apple Inc.", 178.50m), new("MSFT", "Microsoft Corporation", 378.90m), new("NVDA", "NVIDIA Corporation", 495.20m)];
    public IReadOnlyList<Quote> GetQuotes() => Quotes;
    public Quote GetQuote(string symbol) => Quotes.FirstOrDefault(q => q.Symbol == symbol) ?? throw new ApiException(400, "Unsupported demo symbol. See /api/v1/prices.");
}
