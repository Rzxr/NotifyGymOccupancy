using System.Net;
using System.Net.Http.Json;
using NotifyGymOccupancy.Client.Models;
using Microsoft.Extensions.Options;

namespace NotifyGymOccupancy.Client;

/// <summary>
/// Talks to The Gym Group's mobile app API (hosted by Netpulse).
/// Auth is a JSESSIONID cookie obtained from the login endpoint and we re-login when it expires.
/// </summary>
public class GymGroupClient
{
    public static readonly Uri BaseAddress = new("https://thegymgroup.netpulse.com/np/");

    /// <summary>The Gym Group's Android app version on Google Play (7.8.1 as of September 2026).</summary>
    private const string AppVersion = "7.8.1";

    private readonly HttpClient http;
    private readonly GymGroupOptions options;
    private readonly ILogger<GymGroupClient> logger;

    private Session? session;

    public GymGroupClient(HttpClient http, IOptions<GymGroupOptions> options, ILogger<GymGroupClient> logger)
    {
        this.http = http;
        this.options = options.Value;
        this.logger = logger;
    }

    public static void ConfigureHttpClient(HttpClient http)
    {
        http.BaseAddress = BaseAddress;
        http.DefaultRequestHeaders.Add("Accept", "application/json");
        http.DefaultRequestHeaders.Add("User-Agent", "okhttp/3.12.3");
        http.DefaultRequestHeaders.Add("X-NP-API-Version", "1.5");

        // Match the current Google Play release so requests look like the real app
        http.DefaultRequestHeaders.Add("X-NP-App-Version", AppVersion);
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-NP-User-Agent",
            $"clientType=MOBILE_DEVICE; devicePlatform=ANDROID; deviceUid=; applicationName=The Gym Group; applicationVersion={AppVersion}; applicationVersionCode=38");
    }

    public async Task<GymBusyness> GetCapacityAsync(CancellationToken cancellationToken)
    {
        session ??= await LoginAsync(cancellationToken);

        using var response = await SendBusynessRequestAsync(session, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogInformation("Session expired, logging in again");
            session = await LoginAsync(cancellationToken);

            using var retry = await SendBusynessRequestAsync(session, cancellationToken);
            return await ReadBusynessAsync(retry, cancellationToken);
        }

        return await ReadBusynessAsync(response, cancellationToken);
    }

    private Task<HttpResponseMessage> SendBusynessRequestAsync(Session session, CancellationToken cancellationToken)
    {
        var gymId = options.GymLocationId ?? session.HomeGymId;

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"thegymgroup/v1.0/exerciser/{session.UserId}/gym-busyness?gymLocationId={Uri.EscapeDataString(gymId)}");

        request.Headers.Add("Cookie", session.Cookie);

        return http.SendAsync(request, cancellationToken);
    }

    private static async Task<GymBusyness> ReadBusynessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BusynessResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Empty gym-busyness response");
        return new GymBusyness(body.GymLocationName ?? body.GymLocationId, body.CurrentCapacity, body.CurrentPercentage);
    }

    private async Task<Session> LoginAsync(CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = options.Email,
            ["password"] = options.Pin,
        });
        using var response = await http.PostAsync("exerciser/login", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var cookie = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
            ? setCookies.Select(c => c.Split(';', 2)[0]).FirstOrDefault(c => c.StartsWith("JSESSIONID=", StringComparison.Ordinal))
            : null;
        if (cookie is null)
            throw new InvalidOperationException("Login response did not include a JSESSIONID cookie");

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Empty login response");

        logger.LogInformation("Logged in to The Gym Group (home gym: {HomeGym})", body.HomeClubName);
        return new Session(body.Uuid, body.HomeClubUuid, cookie);
    }
}
