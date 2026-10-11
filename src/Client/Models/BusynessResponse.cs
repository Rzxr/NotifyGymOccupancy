namespace NotifyGymOccupancy.Client.Models;

internal record BusynessResponse(string GymLocationId, string? GymLocationName, int CurrentCapacity, int CurrentPercentage);
