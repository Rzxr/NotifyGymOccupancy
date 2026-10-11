using NotifyGymOccupancy.Client;
using Microsoft.Extensions.Options;
using NotifyGymOccupancy.History;
using NotifyGymOccupancy.Messaging;
using NotifyGymOccupancy.Scheduling;

namespace NotifyGymOccupancy;

public class Worker : BackgroundService
{
    // Each gap between polls is picked at random from this range so requests don't land like clockwork
    private static readonly TimeSpan MinPollInterval = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan MaxPollInterval = TimeSpan.FromMinutes(30);

    // Percentage points from the usual level before we call it quieter or busier than usual
    private const int UsualMargin = 10;

    private readonly ILogger<Worker> logger;
    private readonly GymGroupClient gym;
    private readonly IMessenger messenger;
    private readonly BankHolidayCalendar bankHolidays;
    private readonly OccupancyHistory history;

    private readonly ScheduleOptions schedule;

    public Worker(
        ILogger<Worker> logger,
        GymGroupClient gym,
        IMessenger messenger,
        BankHolidayCalendar bankHolidays,
        OccupancyHistory history,
        IOptions<ScheduleOptions> schedule)
    {
        this.logger = logger;
        this.gym = gym;
        this.messenger = messenger;
        this.bankHolidays = bankHolidays;
        this.history = history;
        this.schedule = schedule.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Check the clock every minute rather than sleeping until the next window,
        // so polling resumes on time after the PC wakes from sleep
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        DateTime? nextPoll = null;
        var wasActive = true;
        do
        {
            await RefreshBankHolidaysAsync(cancellationToken);

            var now = DateTime.Now;
            var active = schedule.IsActive(now, bankHolidays.IsBankHoliday(DateOnly.FromDateTime(now)));
            if (!active)
            {
                if (wasActive)
                    logger.LogInformation("Outside the schedule, next poll at {NextStart:ddd dd MMM HH:mm}", schedule.NextStart(now, bankHolidays.IsBankHoliday));
                wasActive = false;
                continue;
            }

            wasActive = true;
            if (now < nextPoll)
                continue;

            nextPoll = now + RandomPollInterval();
            try
            {
                await SendMessage(now, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Poll failed");
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private static TimeSpan RandomPollInterval() =>
        MinPollInterval + (MaxPollInterval - MinPollInterval) * Random.Shared.NextDouble();

    private async Task RefreshBankHolidaysAsync(CancellationToken cancellationToken)
    {
        try
        {
            await bankHolidays.RefreshIfStaleAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Bank holiday refresh failed");
        }
    }

    private string FormatMessage(OccupancyReading reading, int? usual)
    {
        var message = $"{reading.GymName}: {reading.Capacity} people ({reading.Percentage}%) \n{BusyLabel(reading.Percentage)}";
        if (usual is null)
            return message;

        var comparison = (reading.Percentage - usual.Value) switch
        {
            < -UsualMargin => "quieter than usual",
            > UsualMargin => "busier than usual",
            _ => "about as busy as usual",
        };
        var day = bankHolidays.IsBankHoliday(DateOnly.FromDateTime(reading.Time)) ? "bank holidays" : $"{reading.Time.DayOfWeek}s";

        return $"{message}\n{char.ToUpper(comparison[0])}{comparison[1..]} (usually {usual}% around now on {day})";
    }

    private static string BusyLabel(int percentage) => percentage switch
    {
        < 30 => "🟢 Quiet",
        < 40 => "🟡 Moderate",
        _ => "🔴 Busy",
    };

    private async Task SendMessage(DateTime now, CancellationToken cancellationToken)
    {
        var busyness = await gym.GetCapacityAsync(cancellationToken);
        logger.LogInformation("{Gym}: {Capacity} people ({Percentage}%)", busyness.GymName, busyness.Capacity, busyness.Percentage);

        var reading = new OccupancyReading(now, busyness.GymName, busyness.Capacity, busyness.Percentage);
        
        var usual = history.UsualPercentage(reading, bankHolidays.IsBankHoliday);        
        await history.AddAsync(reading, cancellationToken);

        await messenger.SendAsync(FormatMessage(reading, usual), cancellationToken);
    }    
}
