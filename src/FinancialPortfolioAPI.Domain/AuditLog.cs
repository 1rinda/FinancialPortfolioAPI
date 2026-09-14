namespace FinancialPortfolioAPI.Domain;
public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityName { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public string? BeforeJson { get; set; }
    public string AfterJson { get; set; } = "";
    public string Actor { get; set; } = "";
    public string? IpAddress { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
public sealed class PriceHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Symbol { get; set; } = "";
    public decimal Price { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string Source { get; set; } = "Fixed demo quote";
}
