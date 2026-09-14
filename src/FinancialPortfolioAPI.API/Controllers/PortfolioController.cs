using FinancialPortfolioAPI.Application;
using FinancialPortfolioAPI.Domain;
using Microsoft.AspNetCore.Mvc;
namespace FinancialPortfolioAPI.API.Controllers;

[ApiController]
[Route("api/v1/portfolio")]
public sealed class PortfolioController(PortfolioService service) : ControllerBase
{
    [HttpGet]
    public IActionResult List(string? clientId = null, PortfolioStatus? status = null, RiskProfile? riskProfile = null, int offset = 0, int limit = 50) {
        if (status.HasValue && !Enum.IsDefined(status.Value) || riskProfile.HasValue && !Enum.IsDefined(riskProfile.Value)) return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid filter." });
        return Ok(service.List(clientId, status, riskProfile, offset, limit));
    }
    [HttpPost]
    public IActionResult Create(CreatePortfolioRequest request) {
        var p = service.Create(request); return CreatedAtAction(nameof(Get), new { id = p.Id }, p);
    }
    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id) => Ok(service.Get(id));
    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, UpdatePortfolioRequest request) => Ok(service.Update(id, request));
    [HttpPost("{id:guid}/recalculate")]
    public IActionResult Recalculate(Guid id, VersionRequest request) => Ok(service.Recalculate(id, request.ExpectedVersion));
    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id, [FromQuery] long expectedVersion) {
        if (expectedVersion < 1) return BadRequest(new ProblemDetails { Status = 400, Title = "expectedVersion must be positive." });
        return Ok(service.Delete(id, expectedVersion));
    }
}
