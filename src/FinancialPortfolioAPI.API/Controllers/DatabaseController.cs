using FinancialPortfolioAPI.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace FinancialPortfolioAPI.API.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class DatabaseController(Func<PortfolioDbContext> factory) : ControllerBase
{
    [HttpGet("audit")]
    public IActionResult Audit(Guid? entityId = null, int offset = 0, int limit = 50)
    {
        if (offset < 0 || limit is < 1 or > 100) return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid pagination." });
        using var db = factory();
        var query = db.AuditLogs.AsNoTracking().Where(a => entityId == null || a.EntityId == entityId);
        var rows = query.AsEnumerable().OrderByDescending(a => a.Timestamp).ThenBy(a => a.Id).ToList();
        return Ok(new { total = rows.Count, offset, limit, items = rows.Skip(offset).Take(limit).ToList() });
    }
    [HttpGet("prices/{symbol}/history")]
    public IActionResult History(string symbol, int offset = 0, int limit = 50)
    {
        if (offset < 0 || limit is < 1 or > 100) return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid pagination." });
        using var db = factory();
        symbol = symbol.Trim().ToUpperInvariant();
        var rows = db.PriceHistories.AsNoTracking().Where(h => h.Symbol == symbol)
            .AsEnumerable().OrderByDescending(h => h.RecordedAt).ThenBy(h => h.Id).ToList();
        return Ok(new { total = rows.Count, offset, limit, items = rows.Skip(offset).Take(limit).ToList() });
    }
}
