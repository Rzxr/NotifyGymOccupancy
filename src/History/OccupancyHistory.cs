using System.Globalization;

namespace NotifyGymOccupancy.History;

public record OccupancyReading(DateTime Time, string GymName, int Capacity, int Percentage);

/// <summary>
/// Every reading, appended to a CSV file so it survives restarts and can be opened in Excel.
/// </summary>
public class OccupancyHistory
{
    private const string Header = "Time,Capacity,Percentage,Gym";
    private const string TimeFormat = "yyyy-MM-dd HH:mm";

    // How far back and how close in time of day a reading must be to count towards "usual"
    private static readonly TimeSpan Lookback = TimeSpan.FromDays(90);
    private static readonly TimeSpan Nearby = TimeSpan.FromMinutes(30);
    private const int MinimumSamples = 3;

    private readonly string path = Path.Combine(AppPaths.DataDirectory, "history.csv");
    private readonly List<OccupancyReading> readings;

    public OccupancyHistory()
    {
        readings = File.Exists(path)
            ? File.ReadLines(path).Skip(1).Select(TryParse).OfType<OccupancyReading>().ToList()
            : [];
    }

    public async Task AddAsync(OccupancyReading reading, CancellationToken cancellationToken)
    {
        var line = string.Join(',',
            reading.Time.ToString(TimeFormat, CultureInfo.InvariantCulture),
            reading.Capacity,
            reading.Percentage,
            reading.GymName);

        var text = File.Exists(path) ? line + Environment.NewLine : Header + Environment.NewLine + line + Environment.NewLine;
        await File.AppendAllTextAsync(path, text, cancellationToken);
        readings.Add(reading);
    }

    /// <summary>
    /// Median percentage at this gym around this time of day on earlier days of the same kind
    /// (the same weekday, or any bank holiday), or null if there aren't enough readings yet.
    /// </summary>
    public int? UsualPercentage(OccupancyReading current, Func<DateOnly, bool> isBankHoliday)
    {
        var kind = DayKind(DateOnly.FromDateTime(current.Time), isBankHoliday);

        var samples = readings
            .Where(r => r.GymName == current.GymName
                && r.Time.Date < current.Time.Date
                && current.Time - r.Time <= Lookback
                && (r.Time.TimeOfDay - current.Time.TimeOfDay).Duration() <= Nearby
                && DayKind(DateOnly.FromDateTime(r.Time), isBankHoliday) == kind)
            .Select(r => r.Percentage)
            .Order()
            .ToList();

        return samples.Count >= MinimumSamples ? samples[samples.Count / 2] : null;
    }

    private static DayOfWeek? DayKind(DateOnly date, Func<DateOnly, bool> isBankHoliday) =>
        isBankHoliday(date) ? null : date.DayOfWeek;

    private static OccupancyReading? TryParse(string line)
    {
        // The gym name is last so a comma in it doesn't break the split
        var parts = line.Split(',', 4);
        return parts.Length == 4
            && DateTime.TryParseExact(parts[0], TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            && int.TryParse(parts[1], CultureInfo.InvariantCulture, out var capacity)
            && int.TryParse(parts[2], CultureInfo.InvariantCulture, out var percentage)
            ? new OccupancyReading(time, parts[3], capacity, percentage)
            : null;
    }
}
