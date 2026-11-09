namespace RaceDay.Api.DTOs;

public record UserProfileResponse(int UserId, string FirstName, string LastName, string Email, string Role, DateTime CreatedAt, string? ProfilePictureUrl);

public record UpdateProfileRequest(string FirstName, string LastName, string Email);
