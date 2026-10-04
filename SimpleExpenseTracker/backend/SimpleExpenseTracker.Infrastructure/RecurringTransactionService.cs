using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Infrastructure;

public sealed class RecurringTransactionService(ExpenseDbContext db, ILocalDateProvider clock, RecurringTransactionGenerator generator) : IRecurringTransactionService
{
    private IQueryable<RecurringTransaction> Rows => generator.Rows;
    private static AppException Missing() => new(404, "找不到這個固定收支項目。");
    private static AppException Conflict() => new(409, "固定收支已更新，請載入最新內容後再操作。");
    private static IQueryable<RecurringTransaction> Filter(IQueryable<RecurringTransaction> rows, RecurringQuery q)
    {
        if (q.MemberId <= 0 || (q.Ownership.HasValue && !Enum.IsDefined(q.Ownership.Value)) || (q.MemberId.HasValue && q.Ownership is not null and not OwnershipKind.Personal)) throw new AppException(400, "收支歸屬篩選不正確。");
        if (q.MemberId.HasValue) rows = rows.Where(r => r.MemberId == q.MemberId);
        if (q.Ownership == OwnershipKind.Shared) rows = rows.Where(r => r.MemberId == null);
        if (q.Ownership == OwnershipKind.Personal) rows = rows.Where(r => r.MemberId != null);
        if (q.Ownership == OwnershipKind.Unknown) rows = rows.Where(r => false);
        if (q.Type.HasValue) rows = rows.Where(r => r.Type == q.Type);
        if (q.IsActive.HasValue) rows = rows.Where(r => r.IsActive == q.IsActive);
        return rows;
    }
    public async Task<IReadOnlyList<RecurringDto>> List(RecurringQuery query, CancellationToken ct) => (await Filter(Rows.AsNoTracking(), query).OrderByDescending(r => r.IsActive).ThenBy(r => r.NextRunDate).ThenBy(r => r.Id).ToListAsync(ct)).Select(RecurringRules.Map).ToArray();
    public async Task<RecurringDto> Get(int id, CancellationToken ct) => RecurringRules.Map(await Rows.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw Missing());
    public async Task<RecurringDto> Save(int? id, RecurringInput input, CancellationToken ct)
    {
        if (input.StartDate is null || input.EndDate < input.StartDate || input.EndDate?.Year > 9998 || string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100 || (input.Note?.Length ?? 0) > 500
            || input.Amount <= 0 || input.Amount > 999999999999.99m || decimal.Round(input.Amount, 2) != input.Amount || !Enum.IsDefined(input.Type) || !Enum.IsDefined(input.AmountType)) throw new AppException(400, "請檢查固定收支名稱、金額與日期範圍。");
        var schedule = new RecurringSchedule(input.Frequency, input.Interval, input.StartDate.Value, input.DayOfMonth, input.DayOfWeek, input.MonthOfYear); schedule.Validate();
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input with { Version = null }))));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!id.HasValue && input.ClientRequestId.HasValue)
        {
            if (input.ClientRequestId == Guid.Empty) throw new AppException(400, "送出識別碼不正確。");
            var prior = await Rows.AsNoTracking().SingleOrDefaultAsync(r => r.ClientRequestId == input.ClientRequestId, ct);
            if (prior is not null)
            {
                if (prior.CreationHash != fingerprint) throw new AppException(409, "此送出識別碼已使用於不同內容，請確認上一筆結果。");
                return RecurringRules.Map(prior);
            }
        }
        var r = id.HasValue ? await Rows.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw Missing() : new RecurringTransaction { CreatedAt = clock.UtcNow };
        if (id.HasValue && input.Version != r.Version) throw Conflict();
        var category = await db.Categories.FindAsync([input.CategoryId], ct) ?? throw new AppException(400, "分類不存在。");
        var account = await db.Accounts.FindAsync([input.AccountId], ct) ?? throw new AppException(400, "帳戶不存在。");
        var member = input.MemberId.HasValue ? await db.Members.FindAsync([input.MemberId.Value], ct) ?? throw new AppException(400, "歸屬成員不存在。") : null;
        if (category.Type != input.Type || (!category.IsActive && (!id.HasValue || r.CategoryId != category.Id)) || (!account.IsActive && (!id.HasValue || r.AccountId != account.Id)) || (member is { IsActive: false } && (!id.HasValue || r.MemberId != member.Id)))
            throw new AppException(400, "請選擇類型相符且啟用的分類、帳戶與歸屬成員。");
        var oldSchedule = id.HasValue ? RecurringRules.Schedule(r) : null;
        var wasActive = r.IsActive;
        if (id.HasValue && wasActive && input.IsActive)
        {
            var settled = await generator.GenerateRule(r, clock.Today, ct);
            if (settled.HasMore) throw new AppException(409, "尚有到期帳目待補產生，請先執行補產生後再修改。");
        }
        r.Name = input.Name.Trim(); r.Type = input.Type; r.Category = category; r.Account = account; r.Member = member; r.MemberId = member?.Id;
        r.Amount = input.Amount; r.AmountType = input.AmountType; r.Frequency = input.Frequency; r.Interval = input.Interval;
        r.DayOfMonth = input.DayOfMonth; r.DayOfWeek = input.DayOfWeek; r.MonthOfYear = input.MonthOfYear;
        r.StartDate = input.StartDate.Value; r.EndDate = input.EndDate; r.IsActive = input.IsActive; r.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(); r.UpdatedAt = clock.UtcNow;
        if (!id.HasValue) { r.NextRunDate = RecurringRules.Bound(r, schedule.First()); r.ClientRequestId = input.ClientRequestId; r.CreationHash = fingerprint; db.RecurringTransactions.Add(r); }
        else
        {
            r.Version++;
            if (oldSchedule != schedule || (!wasActive && r.IsActive) || !r.NextRunDate.HasValue)
            {
                var cutoff = clock.Today;
                if (r.LastGeneratedDate >= cutoff) cutoff = r.LastGeneratedDate.Value.AddDays(1);
                r.NextRunDate = RecurringRules.Bound(r, schedule.OnOrAfter(cutoff));
            }
            else r.NextRunDate = RecurringRules.Bound(r, r.NextRunDate);
        }
        await SaveChanges(ct); await transaction.CommitAsync(ct); return RecurringRules.Map(r);
    }
    public async Task<RecurringDto> Status(int id, RecurringStatus input, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var r = await Rows.SingleOrDefaultAsync(r => r.Id == id, ct) ?? throw Missing();
        if (r.Version != input.Version) throw Conflict();
        if (input.IsActive && !r.IsActive)
        {
            var cutoff = clock.Today;
            if (r.LastGeneratedDate >= cutoff) cutoff = r.LastGeneratedDate.Value.AddDays(1);
            r.NextRunDate = RecurringRules.Bound(r, RecurringRules.Schedule(r).OnOrAfter(cutoff));
        }
        r.IsActive = input.IsActive; r.Version++; r.UpdatedAt = clock.UtcNow;
        await SaveChanges(ct); await transaction.CommitAsync(ct); return RecurringRules.Map(r);
    }
    private async Task SaveChanges(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw Conflict(); }
    }
    public async Task<RecurringSummaryDto> Summary(RecurringQuery query, int? year, int? month, CancellationToken ct)
    {
        var today = clock.Today; var y = year ?? today.Year; var m = month ?? today.Month;
        if (y is < 1 or > 9998 || m is < 1 or > 12) throw new AppException(400, "請提供有效的年份與月份。");
        var rules = await Filter(Rows.AsNoTracking(), query).Where(r => r.IsActive).ToListAsync(ct);
        var monthStart = new DateOnly(y, m, 1); var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var yearStart = new DateOnly(y, 1, 1); var yearEnd = new DateOnly(y, 12, 31);
        decimal monthlyExpense = 0, monthlyIncome = 0, annualExpense = 0, annualIncome = 0;
        foreach (var r in rules)
        {
            var end = r.EndDate.HasValue && r.EndDate < yearEnd ? r.EndDate.Value : yearEnd;
            var dates = RecurringRules.Schedule(r).Between(yearStart, end).ToArray();
            var monthly = dates.Count(d => d >= monthStart && d <= monthEnd) * r.Amount; var annual = dates.Length * r.Amount;
            if (r.Type == TransactionType.Expense) { monthlyExpense += monthly; annualExpense += annual; }
            else { monthlyIncome += monthly; annualIncome += annual; }
        }
        return new(monthlyExpense, monthlyIncome, annualExpense, annualIncome, rules.Count, y, m);
    }
    public async Task<IReadOnlyList<UpcomingDto>> Upcoming(RecurringQuery query, int days, CancellationToken ct)
    {
        if (days is < 1 or > 366) throw new AppException(400, "查詢天數需介於 1 至 366。");
        var end = clock.Today.AddDays(days - 1);
        return await Filter(Rows.AsNoTracking(), query).Where(r => r.IsActive && r.NextRunDate.HasValue && r.NextRunDate <= end)
            .OrderBy(r => r.NextRunDate).ThenBy(r => r.Id).Select(r => new UpcomingDto(r.Id, r.Name, r.NextRunDate!.Value, r.Amount, r.Type, r.AmountType, r.MemberId, r.Member != null ? r.Member.Name : null)).ToArrayAsync(ct);
    }
}
