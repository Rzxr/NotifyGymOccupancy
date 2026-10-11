namespace NotifyGymOccupancy;

public static class AppPaths
{
    /// <summary>%LOCALAPPDATA%\NotifyGymOccupancy, shared by debug runs and the published release.</summary>
    public static string DataDirectory { get; } = Directory.CreateDirectory(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotifyGymOccupancy")).FullName;
}
