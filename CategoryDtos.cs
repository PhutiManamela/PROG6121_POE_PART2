namespace RaceDay.Api.DTOs;

public record CategoryResponse(int CategoryId, int EventId, string Name, decimal DistanceKm, int MaxParticipants, decimal EntryFee);

public record CreateCategoryRequest(string Name, decimal DistanceKm, int MaxParticipants, decimal EntryFee);

public record UpdateCategoryRequest(string Name, decimal DistanceKm, int MaxParticipants, decimal EntryFee);
