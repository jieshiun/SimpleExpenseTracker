using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Infrastructure;

public static class RecurringRules
{
    public static RecurringSchedule Schedule(RecurringTransaction r) => new(r.Frequency, r.Interval, r.StartDate, r.DayOfMonth, r.DayOfWeek, r.MonthOfYear);
    public static DateOnly? Bound(RecurringTransaction r, DateOnly? date) => date.HasValue && (!r.EndDate.HasValue || date <= r.EndDate) ? date : null;
    public static string? Warning(RecurringTransaction r) => !r.Category.IsActive ? "分類已停用，暫停產生；請修改分類或重新啟用。" : !r.Account.IsActive ? "帳戶已停用，暫停產生；請修改帳戶或重新啟用。" : r.Member is { IsActive: false } ? "歸屬成員已停用，暫停產生；請修改歸屬或重新啟用。" : r.Category.Type != r.Type ? "分類類型不符，暫停產生。" : null;
    public static RecurringDto Map(RecurringTransaction r) => new(r.Id, r.Name, r.Type, r.CategoryId, r.Category.Name, r.Category.Icon, r.AccountId, r.Account.Name, r.MemberId, r.Member?.Name, r.Amount, r.AmountType, r.Frequency, r.Interval, r.DayOfMonth, r.DayOfWeek, r.MonthOfYear, r.StartDate, r.EndDate, r.NextRunDate, r.LastGeneratedDate, r.IsActive, r.Note, r.Version, Warning(r));
}
