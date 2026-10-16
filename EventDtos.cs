namespace RaceDay.Api.DTOs;

public record EventResponse(
    int EventId,
    string Name,
    string? Description,
    DateTime EventDate,
    string Location,
    int OrganiserId,
    string OrganiserName,
    List<CategoryResponse> Categories);

public record EventSummaryResponse(int EventId, string Name, DateTime EventDate, string Location, int OrganiserId);

public record CreateEventRequest(string Name, string? Description, DateTime EventDate, string Location);

public record UpdateEventRequest(string Name, string? Description, DateTime EventDate, string Location);
