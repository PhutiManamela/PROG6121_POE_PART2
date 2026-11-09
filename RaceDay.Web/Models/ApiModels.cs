namespace RaceDay.Web.Models;

// Shapes of the JSON exchanged with the RaceDay API. These are the MVC app's own copies:
// it deliberately does not reference the API project.

public record AuthResponse(int UserId, string FirstName, string LastName, string Email, string Role, string? Token, string? ProfilePictureUrl);

public record UserProfile(int UserId, string FirstName, string LastName, string Email, string Role, DateTime CreatedAt, string? ProfilePictureUrl);

public record CategoryItem(int CategoryId, int EventId, string Name, decimal DistanceKm, int MaxParticipants, decimal EntryFee);

public record EventSummary(int EventId, string Name, DateTime EventDate, string Location, decimal DistanceKm, string EventType, string? BannerUrl, int OrganiserId);

public record EventDetail(
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
    List<CategoryItem> Categories);

public record EnrolmentItem(
    int EnrolmentId,
    int ParticipantId,
    string ParticipantName,
    int CategoryId,
    string CategoryName,
    int EventId,
    string EventName,
    DateTime EnrolmentDate,
    string Status,
    bool HasResult);

public record ResultItem(
    int ResultId,
    int EnrolmentId,
    int ParticipantId,
    string ParticipantName,
    int CategoryId,
    string CategoryName,
    int EventId,
    string EventName,
    TimeSpan FinishTime,
    int? Position,
    int TotalFinishers,
    DateTime RecordedAt);

public record UploadResult(string Url);

// Request bodies
public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string Role);

public record LoginRequest(string Email, string Password);

public record ProfileUpdateRequest(string FirstName, string LastName, string Email);

public record EventRequest(string Name, string? Description, DateTime EventDate, string Location, decimal DistanceKm, string EventType);

public record CategoryRequest(string Name, decimal DistanceKm, int MaxParticipants, decimal EntryFee);

public record ResultRequest(TimeSpan FinishTime, int? Position);
