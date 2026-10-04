using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;
using Xunit;

namespace SimpleExpenseTracker.Tests;

public sealed class RecurringScheduleTests
{
    [Fact]
    public void Monthly31ReturnsToOriginalDayAfterShortMonths()
    {
        var schedule = new RecurringSchedule(RecurringFrequency.Monthly, 1, new(2026, 1, 31), 31);
        Assert.Equal(new[] { new DateOnly(2026, 1, 31), new(2026, 2, 28), new(2026, 3, 31), new(2026, 4, 30) }, schedule.Between(new(2026, 1, 1), new(2026, 4, 30)));
    }

    [Fact]
    public void LeapDayYearlyClampsAndReturnsOnLeapYear()
    {
        var schedule = new RecurringSchedule(RecurringFrequency.Yearly, 1, new(2028, 2, 29), 29, MonthOfYear: 2);
        Assert.Equal(new[] { new DateOnly(2028, 2, 29), new(2029, 2, 28), new(2030, 2, 28), new(2031, 2, 28), new(2032, 2, 29) }, schedule.Between(new(2028, 1, 1), new(2032, 12, 31)));
    }

    [Fact]
    public void WeeklyAndIntervalTwoFollowAnchoredCalendar()
    {
        var weekly = new RecurringSchedule(RecurringFrequency.Weekly, 2, new(2026, 10, 4), DayOfWeek: DayOfWeek.Monday);
        Assert.Equal(new DateOnly(2026, 10, 5), weekly.First());
        Assert.Equal(new DateOnly(2026, 10, 19), weekly.Next(weekly.First()!.Value));
        var monthly = new RecurringSchedule(RecurringFrequency.Monthly, 2, new(2026, 1, 20), 10);
        Assert.Equal(new DateOnly(2026, 3, 10), monthly.First());
        Assert.Equal(new DateOnly(2026, 5, 10), monthly.OnOrAfter(new(2026, 3, 11)));
    }

    [Fact]
    public void BoundedRangeIncludesEndAndCalendarLimitTerminates()
    {
        var schedule = new RecurringSchedule(RecurringFrequency.Monthly, 1, new(2026, 7, 1), 1);
        Assert.Equal(4, schedule.Between(new(2026, 7, 1), new(2026, 10, 1)).Count());
        Assert.Empty(schedule.Between(new(2026, 7, 1), new(2026, 6, 30)));
        Assert.Null(new RecurringSchedule(RecurringFrequency.Monthly, 1, new(9998, 12, 31), 31).Next(new(9998, 12, 31)));
    }
}
