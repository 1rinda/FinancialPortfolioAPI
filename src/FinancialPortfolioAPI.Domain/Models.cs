namespace FinancialPortfolioAPI.Domain;

public enum AssetClass { Equity, FixedIncome, Alternative, Cash, Commodity, RealEstate, Cryptocurrency }
public enum PortfolioStatus { Active, Inactive, PendingReview, Restricted, Closed }
public enum RiskProfile { Conservative, Moderate, Aggressive, VeryAggressive }
public enum TransactionType { Buy, Sell, Dividend, Deposit, Withdrawal }
public enum TransactionStatus { Pending, Executed, Settled, Failed, Cancelled }

public sealed class Portfolio
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public decimal CashBalance { get; set; }
    public PortfolioStatus Status { get; set; }
    public RiskProfile RiskProfile { get; set; }
    public long Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Holding> Holdings { get; set; } = [];
    public decimal TotalValue => CashBalance + Holdings.Sum(h => h.MarketValue);
    public decimal UnrealizedGainLoss => Holdings.Sum(h => h.GainLoss);
    public Dictionary<string, decimal> Allocation => TotalValue == 0 ? [] :
        Holdings.GroupBy(h => h.AssetClass.ToString()).ToDictionary(g => g.Key, g => g.Sum(h => h.MarketValue) / TotalValue * 100)
        .Concat(new[] { new KeyValuePair<string, decimal>("Cash", CashBalance / TotalValue * 100) })
        .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
}
public sealed class Holding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PortfolioId { get; set; }
    public string Symbol { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public AssetClass AssetClass { get; set; } = AssetClass.Equity;
    public decimal Shares { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal CostBasis { get; set; }
    public DateTimeOffset LastPriceUpdate { get; set; }
    public decimal MarketValue => Shares * CurrentPrice;
    public decimal GainLoss => Shares * (CurrentPrice - CostBasis);
    public decimal GainLossPercentage => CostBasis == 0 ? 0 : (CurrentPrice - CostBasis) / CostBasis * 100;
}
public sealed class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PortfolioId { get; set; }
    public string? Symbol { get; set; }
    public TransactionType Type { get; set; }
    public decimal Shares { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public decimal Commission { get; set; }
    public decimal NetAmount { get; set; }
    public string ReferenceNumber { get; set; } = "";
    public string Notes { get; set; } = "";
    public TransactionStatus Status { get; set; }
    public DateTimeOffset TransactionDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SettlementDate { get; set; }
    public long Version { get; set; } = 1;
}
public sealed record Change(long Sequence, string EntityType, Guid EntityId, long Version, DateTimeOffset CommittedAt, string Action = "Updated");
public sealed class Database
{
    public List<Portfolio> Portfolios { get; set; } = [];
    public List<Transaction> Transactions { get; set; } = [];
    public List<Change> Changes { get; set; } = [];
}
