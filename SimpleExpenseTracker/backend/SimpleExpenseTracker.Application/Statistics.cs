using SimpleExpenseTracker.Domain;
namespace SimpleExpenseTracker.Application;
public sealed record SummaryDto(decimal Income, decimal Expense, decimal Balance);
public sealed record CategoryStatisticDto(int CategoryId, string CategoryName, decimal Amount, decimal Percentage);
public sealed record MonthlyStatisticDto(int Year, int Month, decimal Income, decimal Expense);
public interface IStatisticsService
{
    Task<SummaryDto> SummaryAsync(int year, int month, CancellationToken ct, OwnershipKind? ownership = null, int? ownerMemberId = null);
    Task<IReadOnlyList<CategoryStatisticDto>> CategoriesAsync(int year, int month, TransactionType type, CancellationToken ct, OwnershipKind? ownership = null, int? ownerMemberId = null);
    Task<IReadOnlyList<MonthlyStatisticDto>> MonthlyAsync(int months, CancellationToken ct, OwnershipKind? ownership = null, int? ownerMemberId = null);
}
