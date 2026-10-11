using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace NotifyGymOccupancy.Messaging;

/// <summary>
/// Posts messages to a Discord channel through an incoming webhook.
/// </summary>
public class DiscordMessenger : IMessenger
{
    // Discord rejects message content longer than this
    private const int MaxContentLength = 2000;

    private readonly HttpClient http;
    private readonly DiscordOptions options;

    public DiscordMessenger(HttpClient http, IOptions<DiscordOptions> options)
    {
        this.http = http;
        this.options = options.Value;
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        var content = message.Length > MaxContentLength ? message[..MaxContentLength] : message;

        using var response = await http.PostAsJsonAsync(options.WebhookUrl, new { content }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
