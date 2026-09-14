using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FinancialPortfolioAPI.Domain;

namespace FinancialPortfolioAPI.Application;

public sealed record CreatePortfolioRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [Required, StringLength(50, MinimumLength = 1)] string ClientId,
    [Required, StringLength(200, MinimumLength = 1)] string ClientName,
    RiskProfile RiskProfile);
public sealed record UpdatePortfolioRequest(
    [Range(1, long.MaxValue)] long ExpectedVersion,
    [Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: JsonRequired] RiskProfile RiskProfile, [property: JsonRequired] PortfolioStatus Status);
public sealed record CreateTransactionRequest(
    [property: JsonRequired] TransactionType Type,
    [Required, StringLength(50, MinimumLength = 1)] string ReferenceNumber,
    [StringLength(20)] string? Symbol = null,
    [Range(typeof(decimal), "0", "1000000000")] decimal Shares = 0,
    [Range(typeof(decimal), "0", "1000000000")] decimal Price = 0,
    [Range(typeof(decimal), "0", "1000000000000")] decimal Amount = 0,
    [Range(typeof(decimal), "0", "1000000")] decimal Commission = 0,
    [StringLength(500)] string Notes = "");
public sealed record StatusRequest(
    [Range(1, long.MaxValue)] long ExpectedVersion, [property: JsonRequired] TransactionStatus Status);
public sealed record VersionRequest([Range(1, long.MaxValue)] long ExpectedVersion);
public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
public interface IPortfolioStore
{
    T Read<T>(Func<Database, T> action);
    T Write<T>(Func<Database, T> action);
}
public sealed record Quote(string Symbol, string CompanyName, decimal Price);
public interface IPricingService
{
    IReadOnlyList<Quote> GetQuotes();
    Quote GetQuote(string symbol);
}
