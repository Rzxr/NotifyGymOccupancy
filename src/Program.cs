using System.Net;
using NotifyGymOccupancy;
using NotifyGymOccupancy.Client;
using NotifyGymOccupancy.History;
using NotifyGymOccupancy.Messaging;
using NotifyGymOccupancy.Scheduling;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["GymGroup:Email"] = Environment.GetEnvironmentVariable("GymGroupUserName"),
    ["GymGroup:Pin"] = Environment.GetEnvironmentVariable("GymGroupPassword"),
    ["Discord:WebhookUrl"] = Environment.GetEnvironmentVariable("DiscordWebhookUrl"),
});

builder.Services.AddOptions<GymGroupOptions>()
    .Bind(builder.Configuration.GetSection(GymGroupOptions.SectionName))
    .Validate(o => o.Email != "" && o.Pin != "",
        "Gym Group credentials missing. Set the GymGroupUserName and GymGroupPassword environment variables")
    .ValidateOnStart();

builder.Services.AddHttpClient<GymGroupClient>(GymGroupClient.ConfigureHttpClient)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.GZip,
    });

builder.Services.AddOptions<DiscordOptions>()
    .Bind(builder.Configuration.GetSection(DiscordOptions.SectionName))
    .Validate(o => Uri.TryCreate(o.WebhookUrl, UriKind.Absolute, out _),
        "Discord webhook missing. Set the DiscordWebhookUrl environment variable")
    .ValidateOnStart();

builder.Services.AddHttpClient<IMessenger, DiscordMessenger>();

builder.Services.AddOptions<ScheduleOptions>()
    .Bind(builder.Configuration.GetSection(ScheduleOptions.SectionName))
    .Validate(o => o.Windows.All(w => w.Days.Count > 0 && w.Start < w.End) && o.BankHolidayWindows.All(w => w.Start < w.End),
        "Each schedule window needs a Start before its End, and each normal window at least one day")
    .ValidateOnStart();

builder.Services.AddHttpClient<BankHolidayCalendar>();
builder.Services.AddSingleton<OccupancyHistory>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
