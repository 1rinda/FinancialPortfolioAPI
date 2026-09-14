using FinancialPortfolioAPI.Domain;
namespace FinancialPortfolioAPI.Application;

public sealed class PortfolioService(IPortfolioStore store, IPricingService pricing)
{
    public object List(string? clientId, PortfolioStatus? status, RiskProfile? riskProfile, int offset, int limit)
    {
        Page(offset, limit);
        return store.Read(db => {
            var rows = db.Portfolios.Where(p => !p.IsDeleted && (clientId == null || p.ClientId == clientId) &&
                (status == null || p.Status == status) && (riskProfile == null || p.RiskProfile == riskProfile))
                .OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).ToList();
            return new { total = rows.Count, offset, limit, items = rows.Skip(offset).Take(limit).ToList() };
        });
    }
    public Portfolio Get(Guid id) => store.Read(db => Find(db, id));
    public Portfolio Create(CreatePortfolioRequest request) => store.Write(db => {
        ValidEnum(request.RiskProfile);
        var p = new Portfolio { Name = request.Name.Trim(), ClientId = request.ClientId.Trim(),
            ClientName = request.ClientName.Trim(), RiskProfile = request.RiskProfile };
        db.Portfolios.Add(p); Record(db, "Portfolio", p.Id, p.Version); return p;
    });
    public Portfolio Update(Guid id, UpdatePortfolioRequest request) => store.Write(db => {
        var p = Find(db, id); Version(p.Version, request.ExpectedVersion);
        ValidEnum(request.RiskProfile); ValidEnum(request.Status);
        if (p.Status == PortfolioStatus.Closed) throw new ApiException(409, "Closed portfolios cannot be changed.");
        if (request.Status == PortfolioStatus.Closed && (p.CashBalance != 0 || p.Holdings.Count != 0 ||
            db.Transactions.Any(t => t.PortfolioId == id && t.Status is TransactionStatus.Pending or TransactionStatus.Executed)))
            throw new ApiException(409, "Empty the portfolio and resolve outstanding transactions before closing.");
        p.Name = request.Name.Trim(); p.RiskProfile = request.RiskProfile; p.Status = request.Status;
        Touch(db, p); return p;
    });
    public Portfolio Recalculate(Guid id, long expectedVersion) => store.Write(db => {
        var p = Find(db, id); Version(p.Version, expectedVersion);
        if (p.Status == PortfolioStatus.Closed) throw new ApiException(409, "Portfolio is closed.");
        foreach (var h in p.Holdings) { h.CurrentPrice = pricing.GetQuote(h.Symbol).Price; h.LastPriceUpdate = DateTimeOffset.UtcNow; }
        Touch(db, p); return p;
    });
    public object Changes(long after, int limit) {
        if (after < 0) throw new ApiException(400, "after must be nonnegative.");
        Page(0, limit);
        return store.Read(db => {
            var changes = db.Changes.Where(c => c.Sequence > after).Take(limit).ToList();
            return new { items = changes, nextCursor = changes.LastOrDefault()?.Sequence ?? after };
        });
    }
    public Transaction GetTransaction(Guid id) => store.Read(db => FindTransaction(db, id));
    public object Transactions(Guid portfolioId, int offset, int limit) {
        Page(offset, limit);
        return store.Read(db => {
            Find(db, portfolioId);
            var rows = db.Transactions.Where(t => t.PortfolioId == portfolioId).OrderBy(t => t.TransactionDate).ThenBy(t => t.Id).ToList();
            return new { total = rows.Count, offset, limit, items = rows.Skip(offset).Take(limit).ToList() };
        });
    }
    public Transaction CreateTransaction(Guid portfolioId, CreateTransactionRequest r) => store.Write(db => {
        var p = Find(db, portfolioId);
        ValidEnum(r.Type);
        if (new[] { r.Shares, r.Price, r.Amount, r.Commission }.Any(v => v < 0 || decimal.Round(v, 6) != v))
            throw new ApiException(400, "Amounts, shares, prices and commission must be nonnegative with at most six decimal places.");
        var symbol = r.Symbol?.Trim().ToUpperInvariant();
        var reference = r.ReferenceNumber.Trim();
        var existing = db.Transactions.FirstOrDefault(t => t.PortfolioId == portfolioId && t.ReferenceNumber == reference);
        if (existing != null) {
            if (existing.Type != r.Type || existing.Symbol != symbol || existing.Shares != r.Shares ||
                existing.Price != r.Price || existing.Amount != r.Amount || existing.Commission != r.Commission || existing.Notes != r.Notes)
                throw new ApiException(409, "Reference number already used for a different request.");
            return existing;
        }
        if (p.Status != PortfolioStatus.Active) throw new ApiException(409, "Portfolio must be active.");
        var trade = r.Type is TransactionType.Buy or TransactionType.Sell;
        if (trade) {
            if (r.Shares <= 0 || r.Price <= 0 || r.Amount != 0 || string.IsNullOrWhiteSpace(symbol))
                throw new ApiException(400, "Trades require symbol, positive shares and price, and zero amount.");
            pricing.GetQuote(symbol);
            if (r.Type == TransactionType.Sell && r.Commission > r.Shares * r.Price)
                throw new ApiException(400, "Commission exceeds sale proceeds.");
        } else if (r.Amount <= 0 || r.Shares != 0 || r.Price != 0 || r.Commission != 0 || !string.IsNullOrEmpty(symbol))
            throw new ApiException(400, "Cash transactions require positive amount; omit symbol, shares, price and commission.");
        var net = r.Type switch {
            TransactionType.Buy => -(r.Shares * r.Price + r.Commission),
            TransactionType.Sell => r.Shares * r.Price - r.Commission,
            TransactionType.Withdrawal => -r.Amount,
            _ => r.Amount
        };
        var t = new Transaction { PortfolioId = portfolioId, Type = r.Type, ReferenceNumber = reference,
            Symbol = symbol, Shares = r.Shares, Price = r.Price, Amount = r.Amount,
            Commission = r.Commission, NetAmount = net, Notes = r.Notes };
        db.Transactions.Add(t); Record(db, "Transaction", t.Id, t.Version); return t;
    });
    public Transaction SetStatus(Guid id, StatusRequest r) => store.Write(db => {
        var t = FindTransaction(db, id); Version(t.Version, r.ExpectedVersion); ValidEnum(r.Status);
        var allowed = t.Status switch {
            TransactionStatus.Pending => r.Status is TransactionStatus.Executed or TransactionStatus.Cancelled or TransactionStatus.Failed,
            TransactionStatus.Executed => r.Status is TransactionStatus.Settled or TransactionStatus.Failed,
            _ => false
        };
        if (!allowed) throw new ApiException(409, "Invalid transaction status transition.");
        var p = Find(db, t.PortfolioId);
        if (r.Status is TransactionStatus.Executed or TransactionStatus.Settled && p.Status != PortfolioStatus.Active)
            throw new ApiException(409, "Portfolio must be active.");
        if (r.Status == TransactionStatus.Settled) {
            if (p.CashBalance + t.NetAmount < 0) throw new ApiException(409, "Insufficient cash.");
            var h = p.Holdings.SingleOrDefault(h => h.Symbol == t.Symbol);
            if (t.Type == TransactionType.Sell) {
                if (h == null || h.Shares < t.Shares) throw new ApiException(409, "Insufficient shares.");
                h.Shares -= t.Shares; if (h.Shares == 0) p.Holdings.Remove(h);
            }
            if (t.Type == TransactionType.Buy) {
                var quote = pricing.GetQuote(t.Symbol!);
                if (h == null) { h = new Holding { Symbol = quote.Symbol, CompanyName = quote.CompanyName }; p.Holdings.Add(h); }
                h.CostBasis = decimal.Round((h.Shares * h.CostBasis + t.Shares * t.Price + t.Commission) / (h.Shares + t.Shares), 12, MidpointRounding.ToEven);
                h.Shares += t.Shares; h.CurrentPrice = quote.Price; h.LastPriceUpdate = DateTimeOffset.UtcNow;
            }
            p.CashBalance += t.NetAmount; t.SettlementDate = DateTimeOffset.UtcNow; Touch(db, p);
        }
        t.Status = r.Status; t.Version++; Record(db, "Transaction", t.Id, t.Version); return t;
    });
    public Portfolio Delete(Guid id, long expectedVersion) => store.Write(db => {
        var p = Find(db, id); Version(p.Version, expectedVersion);
        if (p.CashBalance != 0 || p.Holdings.Count != 0 || db.Transactions.Any(t => t.PortfolioId == id && t.Status is TransactionStatus.Pending or TransactionStatus.Executed))
            throw new ApiException(409, "Empty the portfolio and resolve outstanding transactions before deletion.");
        p.IsDeleted = true; p.DeletedAt = DateTimeOffset.UtcNow; p.Status = PortfolioStatus.Closed;
        p.Version++; p.LastUpdatedAt = DateTimeOffset.UtcNow;
        db.Changes.Add(new Change((db.Changes.LastOrDefault()?.Sequence ?? 0) + 1, "Portfolio", p.Id, p.Version, DateTimeOffset.UtcNow, "Deleted"));
        return p;
    });
    static Portfolio Find(Database db, Guid id) => db.Portfolios.Find(p => p.Id == id && !p.IsDeleted) ?? throw new ApiException(404, "Portfolio not found.");
    static Transaction FindTransaction(Database db, Guid id) => db.Transactions.Find(t => t.Id == id) ?? throw new ApiException(404, "Transaction not found.");
    static void Version(long actual, long expected) { if (actual != expected) throw new ApiException(409, "Version conflict. Retrieve the latest resource and retry."); }
    static void ValidEnum<T>(T value) where T : struct, Enum { if (!Enum.IsDefined(value)) throw new ApiException(400, "Invalid enum value."); }
    static void Page(int offset, int limit) { if (offset < 0 || limit is < 1 or > 100) throw new ApiException(400, "offset must be nonnegative and limit between 1 and 100."); }
    static void Touch(Database db, Portfolio p) { p.Version++; p.LastUpdatedAt = DateTimeOffset.UtcNow; Record(db, "Portfolio", p.Id, p.Version); }
    static void Record(Database db, string type, Guid id, long version) => db.Changes.Add(new Change((db.Changes.LastOrDefault()?.Sequence ?? 0) + 1, type, id, version, DateTimeOffset.UtcNow));
}
