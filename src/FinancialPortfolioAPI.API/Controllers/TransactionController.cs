using FinancialPortfolioAPI.Application;
using Microsoft.AspNetCore.Mvc;
namespace FinancialPortfolioAPI.API.Controllers;
[ApiController]
[Route("api/v1/transaction")]
public sealed class TransactionController(PortfolioService service) : ControllerBase
{
    [HttpGet("portfolio/{portfolioId:guid}")]
    public IActionResult List(Guid portfolioId, int offset = 0, int limit = 50) => Ok(service.Transactions(portfolioId, offset, limit));
    [HttpPost("portfolio/{portfolioId:guid}")]
    public IActionResult Create(Guid portfolioId, CreateTransactionRequest request) {
        var t = service.CreateTransaction(portfolioId, request);
        return CreatedAtAction(nameof(Get), new { id = t.Id }, t);
    }
    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id) => Ok(service.GetTransaction(id));
    [HttpPatch("{id:guid}/status")]
    public IActionResult Status(Guid id, StatusRequest request) => Ok(service.SetStatus(id, request));
}
[ApiController]
[Route("api/v1")]
public sealed class IntegrationController(PortfolioService service, IPricingService pricing) : ControllerBase
{
    [HttpGet("changes")]
    public IActionResult Changes(long after = 0, int limit = 50) => Ok(service.Changes(after, limit));
    [HttpGet("prices")]
    public IActionResult Prices() => Ok(new { source = "Fixed demonstration prices; not live market data", currency = "USD", items = pricing.GetQuotes() });
}
