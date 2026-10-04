using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Application;

// Keep the original calendar rule: February must not move a monthly 31st to the 28th forever.
public sealed record RecurringSchedule(RecurringFrequency Frequency, int Interval, DateOnly StartDate, int? DayOfMonth = null, DayOfWeek? DayOfWeek = null, int? MonthOfYear = null)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Frequency) || Interval is < 1 or > 120 || StartDate.Year > 9998)
            throw new AppException(400, "請選擇有效的週期、開始日期與間隔（1 至 120）。");
        if (Frequency == RecurringFrequency.Weekly)
        {
            if (!DayOfWeek.HasValue || !Enum.IsDefined(DayOfWeek.Value) || DayOfMonth.HasValue || MonthOfYear.HasValue)
                throw new AppException(400, "每週排程需要指定星期，不可同時指定月份或日期。");
        }
        else if (DayOfMonth is null or < 1 or > 31 || DayOfWeek.HasValue || (Frequency == RecurringFrequency.Yearly ? MonthOfYear is null or < 1 or > 12 : MonthOfYear.HasValue))
            throw new AppException(400, "請指定有效的執行日（1 至 31），每年排程另需指定月份。");
    }

    public DateOnly? First()
    {
        Validate();
        if (Frequency == RecurringFrequency.Weekly)
            return AddDays(StartDate, ((int)DayOfWeek!.Value - (int)StartDate.DayOfWeek + 7) % 7);
        var candidate = CalendarDate(StartDate.Year, Frequency == RecurringFrequency.Yearly ? MonthOfYear!.Value : StartDate.Month);
        return candidate >= StartDate ? candidate : Next(candidate);
    }

    public DateOnly? Next(DateOnly occurrence)
    {
        if (Frequency == RecurringFrequency.Weekly) return AddDays(occurrence, 7 * Interval);
        if (Frequency == RecurringFrequency.Yearly)
            return occurrence.Year + Interval > 9998 ? null : CalendarDate(occurrence.Year + Interval, MonthOfYear!.Value);
        var month = (occurrence.Year - 1) * 12 + occurrence.Month - 1 + Interval;
        return month / 12 + 1 > 9998 ? null : CalendarDate(month / 12 + 1, month % 12 + 1);
    }

    public DateOnly? OnOrAfter(DateOnly cutoff)
    {
        if (cutoff.Year > 9998) return null;
        var occurrence = First();
        if (!occurrence.HasValue || occurrence >= cutoff) return occurrence;
        var first = occurrence.Value;
        if (Frequency == RecurringFrequency.Weekly)
            occurrence = AddDays(first, (cutoff.DayNumber - first.DayNumber) / (7 * Interval) * (7 * Interval));
        else if (Frequency == RecurringFrequency.Monthly)
        {
            var offset = ((cutoff.Year - first.Year) * 12 + cutoff.Month - first.Month) / Interval * Interval;
            var month = (first.Year - 1) * 12 + first.Month - 1 + offset;
            occurrence = CalendarDate(month / 12 + 1, month % 12 + 1);
        }
        else occurrence = CalendarDate(first.Year + (cutoff.Year - first.Year) / Interval * Interval, MonthOfYear!.Value);
        return occurrence.HasValue && occurrence < cutoff ? Next(occurrence.Value) : occurrence;
    }

    public IEnumerable<DateOnly> Between(DateOnly start, DateOnly end)
    {
        for (var occurrence = OnOrAfter(start); occurrence.HasValue && occurrence <= end; occurrence = Next(occurrence.Value))
            yield return occurrence.Value;
    }

    private DateOnly CalendarDate(int year, int month) => new(year, month, Math.Min(DayOfMonth!.Value, DateTime.DaysInMonth(year, month)));
    private static DateOnly? AddDays(DateOnly date, int days) => date.DayNumber + days > new DateOnly(9998, 12, 31).DayNumber ? null : date.AddDays(days);
}
