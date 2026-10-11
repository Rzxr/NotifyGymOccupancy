namespace NotifyGymOccupancy.Messaging;

public class DiscordOptions
{
    public const string SectionName = "Discord";

    /// <summary>Incoming webhook URL from the channel's Integrations settings.</summary>
    public string WebhookUrl { get; set; } = "";
}
