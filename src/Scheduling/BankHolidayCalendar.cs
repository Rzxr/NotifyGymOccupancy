using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NotifyGymOccupancy.Scheduling;

/// <summary>
/// UK bank holidays from GOV.UK, cached on disk and refreshed monthly.
/// </summary>
public class BankHolidayCalendar
{
    private static readonly Uri FeedUri = new("https://www.gov.uk/bank-holidays.json");

    private readonly HttpClient http;
    private readonly ScheduleOptions options;
    private readonly ILogger<BankHolidayCalendar> logger;
    private readonly string cachePath = Path.Combine(AppPaths.DataDirectory, "bank-holidays.json");

    private HashSet<DateOnly> dates = [];
    private DateOnly? downloadedOn;
    private DateOnly? attemptedOn;

    public BankHolidayCalendar(HttpClient http, IOptions<ScheduleOptions> options, ILogger<BankHolidayCalendar> logger)
    {
        this.http = http;
        this.options = options.Value;
        this.logger = logger;
    }

    public bool IsBankHoliday(DateOnly date) => dates.Contains(date);

    /// <summary>
    /// Downloads the feed when the cached copy is from before the latest first Tuesday of a month,
    /// so it refreshes monthly, or on the first run after that Tuesday if the PC was off.
    /// </summary>
    public async Task RefreshIfStaleAsync(CancellationToken cancellationToken)
    {
        if (downloadedOn is null && File.Exists(cachePath))
        {
            downloadedOn = DateOnly.FromDateTime(File.GetLastWriteTime(cachePath));
            dates = Parse(await File.ReadAllTextAsync(cachePath, cancellationToken));
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (downloadedOn >= LatestRefreshDay(today) || attemptedOn == today)
            return;

        // Only try once a day while the download keeps failing
        attemptedOn = today;

        try
        {
            var json = await http.GetStringAsync(FeedUri, cancellationToken);
            dates = Parse(json);
            await File.WriteAllTextAsync(cachePath, json, cancellationToken);
            downloadedOn = today;
            logger.LogInformation("Downloaded bank holidays");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Couldn't download bank holidays, using the cached copy");
        }
    }

    private static DateOnly LatestRefreshDay(DateOnly today)
    {
        var thisMonth = FirstTuesday(today.Year, today.Month);
        if (thisMonth <= today)
            return thisMonth;

        var lastMonth = today.AddMonths(-1);
        return FirstTuesday(lastMonth.Year, lastMonth.Month);
    }

    private static DateOnly FirstTuesday(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return first.AddDays(((int)DayOfWeek.Tuesday - (int)first.DayOfWeek + 7) % 7);
    }

    private HashSet<DateOnly> Parse(string json)
    {
        var divisions = JsonSerializer.Deserialize<Dictionary<string, Division>>(json, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Empty bank holiday feed");

        if (!divisions.TryGetValue(options.BankHolidayDivision, out var division))
            throw new InvalidOperationException(
                $"Unknown bank holiday division '{options.BankHolidayDivision}'. Use one of: {string.Join(", ", divisions.Keys)}");

        return division.Events.Select(e => e.Date).ToHashSet();
    }

    private record Division(List<Event> Events);

    private record Event(DateOnly Date);
}
