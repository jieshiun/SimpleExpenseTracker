using SimpleExpenseTracker.Infrastructure;
using Xunit;

namespace SimpleExpenseTracker.Tests;
public class LocalDateProviderTests
{
    private sealed class FixedClock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 3, 16, 30, 0, TimeSpan.Zero); }
    [Fact]
    public void TodayUsesTaipeiCalendarWhileAuditTimeRemainsUtc()
    {
        var provider = new LocalDateProvider(new FixedClock(), TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei"));
        Assert.Equal(new DateOnly(2026, 10, 4), provider.Today);
        Assert.Equal(new DateTime(2026, 10, 3, 16, 30, 0, DateTimeKind.Utc), provider.UtcNow);
    }
}
