using System.Data;
using System.Text.Json;
using FinancialPortfolioAPI.Application;
using FinancialPortfolioAPI.Domain;
using FinancialPortfolioAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinancialPortfolioAPI.Infrastructure;

// A unit of work retains the existing service contract. Writes acquire the database mutex
// before reading state, so version checks, balances and feed sequences are atomic across processes.
public sealed class EfPortfolioStore(Func<PortfolioDbContext> createContext, Func<(string Actor, string? Ip)>? identity = null) : IPortfolioStore
{
    public T Read<T>(Func<Database, T> action)
    {
        using var context = createContext();
        using var tx = context.Database.BeginTransaction(IsolationLevel.Serializable);
        var result = action(Load(context));
        tx.Commit();
        return result;
    }
    public T Write<T>(Func<Database, T> action)
    {
        using var context = createContext();
        using var tx = context.Database.BeginTransaction(IsolationLevel.Serializable);
        context.Database.ExecuteSqlRaw("UPDATE StoreStates SET Revision = Revision + 1 WHERE Id = 1");
        var state = Load(context);
        var previous = state.Portfolios.ToDictionary(p => p.Id, p => JsonSerializer.Serialize(p));
        var previousTransactions = state.Transactions.ToDictionary(t => t.Id, t => JsonSerializer.Serialize(t));
        var previousPrices = state.Portfolios.SelectMany(p => p.Holdings).ToDictionary(h => h.Id, h => h.LastPriceUpdate);
        var sequence = state.Changes.LastOrDefault()?.Sequence ?? 0;
        var result = action(state);
        foreach (var p in state.Portfolios) {
            if (!previous.ContainsKey(p.Id)) context.Portfolios.Add(p);
            foreach (var h in p.Holdings) {
                h.PortfolioId = p.Id;
                if (context.Entry(h).State == EntityState.Detached) context.Holdings.Add(h);
                if (!previousPrices.TryGetValue(h.Id, out var oldDate) || oldDate != h.LastPriceUpdate)
                    context.PriceHistories.Add(new PriceHistory { Symbol = h.Symbol, Price = h.CurrentPrice, RecordedAt = h.LastPriceUpdate });
            }
        }
        foreach (var t in state.Transactions)
            if (!previousTransactions.ContainsKey(t.Id)) context.Transactions.Add(t);
        var actor = identity?.Invoke() ?? ("system", null);
        foreach (var change in state.Changes.Where(c => c.Sequence > sequence)) {
            context.Changes.Add(change);
            string? before; string after;
            if (change.EntityType == "Portfolio") {
                previous.TryGetValue(change.EntityId, out before);
                after = JsonSerializer.Serialize(state.Portfolios.Single(p => p.Id == change.EntityId));
            } else {
                previousTransactions.TryGetValue(change.EntityId, out before);
                after = JsonSerializer.Serialize(state.Transactions.Single(t => t.Id == change.EntityId));
            }
            context.AuditLogs.Add(new AuditLog { EntityName = change.EntityType, EntityId = change.EntityId,
                Action = change.Action == "Deleted" ? "Deleted" : before == null ? "Created" : "Updated",
                BeforeJson = before, AfterJson = after, Actor = actor.Item1, IpAddress = actor.Item2,
                Timestamp = change.CommittedAt });
        }
        try { context.SaveChanges(); tx.Commit(); }
        catch (DbUpdateConcurrencyException) { throw new ApiException(409, "Database version conflict. Retrieve the latest resource."); }
        return result;
    }
    static Database Load(PortfolioDbContext context) => new() {
        Portfolios = context.Portfolios.IgnoreQueryFilters().Include(p => p.Holdings).ToList(),
        Transactions = context.Transactions.ToList(),
        Changes = context.Changes.OrderBy(c => c.Sequence).ToList()
    };
}
