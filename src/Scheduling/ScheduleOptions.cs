namespace NotifyGymOccupancy.Scheduling;

public class ScheduleOptions
{
    public const string SectionName = "Schedule";

    /// <summary>When to poll, in local time. Polls around the clock when this and <see cref="BankHolidayWindows"/> are empty.</summary>
    public List<ScheduleWindow> Windows { get; set; } = [];

    /// <summary>Replaces <see cref="Windows"/> on bank holidays. When empty, bank holidays follow the normal windows.</summary>
    public List<TimeWindow> BankHolidayWindows { get; set; } = [];

    /// <summary>GOV.UK bank holiday division: england-and-wales, scotland or northern-ireland.</summary>
    public string BankHolidayDivision { get; set; } = "england-and-wales";

    public bool IsActive(DateTime now, bool isBankHoliday) =>
        (Windows.Count == 0 && BankHolidayWindows.Count == 0)
        || WindowsFor(DateOnly.FromDateTime(now), isBankHoliday).Any(w => w.Contains(TimeOnly.FromDateTime(now)));

    /// <summary>The next time a window opens after <paramref name="now"/>, or null if none does in the next week.</summary>
    public DateTime? NextStart(DateTime now, Func<DateOnly, bool> isBankHoliday)
    {
        var starts =
            from offset in Enumerable.Range(0, 8)
            let date = DateOnly.FromDateTime(now).AddDays(offset)
            from window in WindowsFor(date, isBankHoliday(date))
            let start = date.ToDateTime(window.Start)
            where start > now
            select (DateTime?)start;

        return starts.Min();
    }

    private IEnumerable<TimeWindow> WindowsFor(DateOnly date, bool isBankHoliday) =>
        isBankHoliday && BankHolidayWindows.Count > 0
            ? BankHolidayWindows
            : Windows.Where(w => w.Days.Contains(date.DayOfWeek));
}

public class TimeWindow
{
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }

    public bool Contains(TimeOnly time) => time >= Start && time < End;
}

public class ScheduleWindow : TimeWindow
{
    public List<DayOfWeek> Days { get; set; } = [];
}
