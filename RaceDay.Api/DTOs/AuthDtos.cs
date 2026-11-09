namespace RaceDay.Api.DTOs;

/// <summary>Request body to register a new user. Role must be "Organiser" or "Participant".</summary>
public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string Role);

/// <summary>Request body to log in with an existing account.</summary>
public record LoginRequest(string Email, string Password);

/// <summary>
/// Returned after a successful register or login. Token (a JWT) is only set on login: the MVC client sends it as
/// "Authorization: Bearer {token}", while browsers/Swagger can rely on the session cookie instead.
/// </summary>
public record AuthResponse(int UserId, string FirstName, string LastName, string Email, string Role, string? Token = null, string? ProfilePictureUrl = null);
