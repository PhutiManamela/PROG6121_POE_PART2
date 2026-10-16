namespace RaceDay.Api.DTOs;

public record UserProfileResponse(int UserId, string FirstName, string LastName, string Email, string Role, DateTime CreatedAt);

public record UpdateProfileRequest(string FirstName, string LastName, string Email);
