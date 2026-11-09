namespace RaceDay.Api.DTOs;

public record EventResponse(
    int EventId,
    string Name,
    string? Description,
    DateTime EventDate,
    string Location,
    decimal DistanceKm,
    string EventType,
    string? BannerUrl,
    int OrganiserId,
    string OrganiserName,
    List<CategoryResponse> Categories);

public record EventSummaryResponse(
    int EventId,
    string Name,
    DateTime EventDate,
    string Location,
    decimal DistanceKm,
    string EventType,
    string? BannerUrl,
    int OrganiserId);

/// <summary>EventType must be "Run", "Walk" or "Cycle".</summary>
public record CreateEventRequest(string Name, string? Description, DateTime EventDate, string Location, decimal DistanceKm, string EventType);

public record UpdateEventRequest(string Name, string? Description, DateTime EventDate, string Location, decimal DistanceKm, string EventType);

public record UploadResponse(string Url);
