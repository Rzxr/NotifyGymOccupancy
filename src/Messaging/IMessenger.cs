namespace NotifyGymOccupancy.Messaging;

public interface IMessenger
{
    Task SendAsync(string message, CancellationToken cancellationToken);
}
