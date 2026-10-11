namespace NotifyGymOccupancy.Client;

public class GymGroupOptions
{
    public const string SectionName = "GymGroup";

    public string Email { get; set; } = "";
    public string Pin { get; set; } = "";

    /// <summary>Gym to watch. Defaults to the member's home gym when not set.</summary>
    public string? GymLocationId { get; set; }
}