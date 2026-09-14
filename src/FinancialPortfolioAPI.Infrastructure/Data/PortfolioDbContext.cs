using FinancialPortfolioAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinancialPortfolioAPI.Infrastructure.Data;

public class PortfolioDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Change> Changes => Set<Change>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<StoreState> StoreStates => Set<StoreState>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Portfolio>(e => {
            e.HasKey(p => p.Id); e.Property(p => p.Id).ValueGeneratedNever();
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.ClientId).HasMaxLength(50).IsRequired();
            e.Property(p => p.ClientName).HasMaxLength(200).IsRequired();
            e.Property(p => p.Currency).HasMaxLength(3).IsRequired();
            e.Property(p => p.Version).IsConcurrencyToken();
            e.HasIndex(p => p.ClientId); e.HasIndex(p => p.Status); e.HasIndex(p => p.RiskProfile);
            e.HasQueryFilter(p => !p.IsDeleted);
            e.Ignore(p => p.TotalValue); e.Ignore(p => p.UnrealizedGainLoss); e.Ignore(p => p.Allocation);
            e.HasMany(p => p.Holdings).WithOne().HasForeignKey(h => h.PortfolioId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Holding>(e => {
            e.HasKey(h => h.Id); e.Property(h => h.Id).ValueGeneratedNever();
            e.Property(h => h.Symbol).HasMaxLength(20).IsRequired();
            e.Property(h => h.CompanyName).HasMaxLength(200).IsRequired();
            e.HasIndex(h => new { h.PortfolioId, h.Symbol }).IsUnique();
            e.Ignore(h => h.MarketValue); e.Ignore(h => h.GainLoss); e.Ignore(h => h.GainLossPercentage);
        });
        model.Entity<Transaction>(e => {
            e.HasKey(t => t.Id); e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.Symbol).HasMaxLength(20);
            e.Property(t => t.ReferenceNumber).HasMaxLength(50).IsRequired();
            e.Property(t => t.Notes).HasMaxLength(500);
            e.Property(t => t.Version).IsConcurrencyToken();
            e.HasIndex(t => new { t.PortfolioId, t.ReferenceNumber }).IsUnique();
            e.HasIndex(t => t.Status); e.HasIndex(t => t.TransactionDate);
            e.HasOne<Portfolio>().WithMany().HasForeignKey(t => t.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Change>(e => {
            e.HasKey(c => c.Sequence); e.Property(c => c.Sequence).ValueGeneratedNever();
            e.Property(c => c.EntityType).HasMaxLength(50); e.Property(c => c.Action).HasMaxLength(20);
            e.HasIndex(c => new { c.EntityId, c.Version });
        });
        model.Entity<AuditLog>(e => {
            e.HasKey(a => a.Id); e.Property(a => a.Id).ValueGeneratedNever();
            e.Property(a => a.EntityName).HasMaxLength(100); e.Property(a => a.Action).HasMaxLength(20);
            e.Property(a => a.Actor).HasMaxLength(100); e.Property(a => a.IpAddress).HasMaxLength(50);
            e.HasIndex(a => new { a.EntityId, a.Timestamp });
        });
        model.Entity<PriceHistory>(e => {
            e.HasKey(h => h.Id); e.Property(h => h.Id).ValueGeneratedNever();
            e.Property(h => h.Symbol).HasMaxLength(20); e.Property(h => h.Source).HasMaxLength(50);
            e.HasIndex(h => new { h.Symbol, h.RecordedAt });
        });
        model.Entity<StoreState>(e => {
            e.HasKey(s => s.Id); e.Property(s => s.Id).ValueGeneratedNever();
            e.HasData(new StoreState { Id = 1, Revision = 0 });
        });
        // Preserve decimal precision on SQLite (TEXT); use fixed decimal precision on SQL Server.
        foreach (var property in model.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.ClrType == typeof(decimal))) {
            if (Database.IsSqlServer()) { property.SetPrecision(38); property.SetScale(12); }
        }
    }
}
public sealed class SqlitePortfolioDbContext(DbContextOptions<SqlitePortfolioDbContext> options) : PortfolioDbContext(options);
public sealed class SqlServerPortfolioDbContext(DbContextOptions<SqlServerPortfolioDbContext> options) : PortfolioDbContext(options);
public sealed class StoreState { public int Id { get; set; } public long Revision { get; set; } }

public sealed class SqliteDesignFactory : IDesignTimeDbContextFactory<SqlitePortfolioDbContext>
{
    public SqlitePortfolioDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SqlitePortfolioDbContext>()
        .UseSqlite(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? "Data Source=portfolio.db").Options);
}
public sealed class SqlServerDesignFactory : IDesignTimeDbContextFactory<SqlServerPortfolioDbContext>
{
    public SqlServerPortfolioDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SqlServerPortfolioDbContext>()
        .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
            "Server=localhost;Database=FinancialPortfolioDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options);
}
