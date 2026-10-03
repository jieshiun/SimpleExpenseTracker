using Microsoft.EntityFrameworkCore;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;
namespace SimpleExpenseTracker.Infrastructure;
public sealed class StatisticsService(ExpenseDbContext db, TimeProvider clock) : IStatisticsService
{
    private IQueryable<Transaction> Month(int year, int month)
    {
        if (year is < 1 or > 9998 || month is < 1 or > 12) throw new AppException(400, "請提供有效的年份與月份。");
        var start = new DateTime(year, month, 1); var end = start.AddMonths(1);
        return db.Transactions.AsNoTracking().Where(t => t.TransactionDate >= start && t.TransactionDate < end);
    }
    public async Task<SummaryDto> SummaryAsync(int year, int month, CancellationToken ct)
    {
        // SQLite cannot SUM decimal natively without converting to floating point.
        // Materialize only the needed columns and aggregate as decimal in memory.
        var rows = await Month(year, month).Select(t => new { t.Type, t.Amount }).ToListAsync(ct);
        var income = rows.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
        var expense = rows.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
        return new(income, expense, income - expense);
    }
    public async Task<IReadOnlyList<CategoryStatisticDto>> CategoriesAsync(int year, int month, TransactionType type, CancellationToken ct)
    {
        if (!Enum.IsDefined(type)) throw new AppException(400, "請選擇收入或支出。");
        var rows = await Month(year, month).Where(t => t.Type == type).Select(t => new { t.CategoryId, t.Category.Name, t.Amount }).ToListAsync(ct);
        var total = rows.Sum(t => t.Amount);
        return rows.GroupBy(t => new { t.CategoryId, t.Name }).Select(g => new CategoryStatisticDto(g.Key.CategoryId, g.Key.Name, g.Sum(t => t.Amount), total == 0 ? 0 : decimal.Round(g.Sum(t => t.Amount) / total * 100, 2))).OrderByDescending(g => g.Amount).ToArray();
    }
    public async Task<IReadOnlyList<MonthlyStatisticDto>> MonthlyAsync(int months, CancellationToken ct)
    {
        if (months is < 1 or > 120) throw new AppException(400, "月份數量需介於 1 至 120。");
        var today = clock.GetLocalNow(); var last = new DateTime(today.Year, today.Month, 1);
        var start = last.AddMonths(1 - months); var end = last.AddMonths(1);
        var rows = await db.Transactions.AsNoTracking().Where(t => t.TransactionDate >= start && t.TransactionDate < end).Select(t => new { t.TransactionDate, t.Type, t.Amount }).ToListAsync(ct);
        return Enumerable.Range(0, months).Select(i => { var m = start.AddMonths(i); var group = rows.Where(t => t.TransactionDate.Year == m.Year && t.TransactionDate.Month == m.Month).ToArray(); return new MonthlyStatisticDto(m.Year, m.Month, group.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount), group.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)); }).ToArray();
    }
}
