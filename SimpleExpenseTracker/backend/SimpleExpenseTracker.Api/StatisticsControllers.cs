using Microsoft.AspNetCore.Mvc;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;
namespace SimpleExpenseTracker.Api;
[ApiController, Route("api/dashboard")]
public sealed class DashboardController(IStatisticsService service) : ControllerBase
{
    [HttpGet("summary")] public Task<SummaryDto> Summary([FromQuery] int year, [FromQuery] int month, CancellationToken ct, [FromQuery] OwnershipKind? ownership = null, [FromQuery] int? ownerMemberId = null) => service.SummaryAsync(year, month, ct, ownership, ownerMemberId);
}
[ApiController, Route("api/statistics")]
public sealed class StatisticsController(IStatisticsService service) : ControllerBase
{
    [HttpGet("categories")] public Task<IReadOnlyList<CategoryStatisticDto>> Categories([FromQuery] int year, [FromQuery] int month, CancellationToken ct, [FromQuery] TransactionType type = TransactionType.Expense, [FromQuery] OwnershipKind? ownership = null, [FromQuery] int? ownerMemberId = null) => service.CategoriesAsync(year, month, type, ct, ownership, ownerMemberId);
    [HttpGet("monthly")] public Task<IReadOnlyList<MonthlyStatisticDto>> Monthly(CancellationToken ct, [FromQuery] int months = 6, [FromQuery] OwnershipKind? ownership = null, [FromQuery] int? ownerMemberId = null) => service.MonthlyAsync(months, ct, ownership, ownerMemberId);
}
