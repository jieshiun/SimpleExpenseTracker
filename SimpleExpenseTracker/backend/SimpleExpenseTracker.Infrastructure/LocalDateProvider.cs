using SimpleExpenseTracker.Application;

namespace SimpleExpenseTracker.Infrastructure;

public sealed class LocalDateProvider(TimeProvider clock, TimeZoneInfo zone) : ILocalDateProvider
{
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;
}
