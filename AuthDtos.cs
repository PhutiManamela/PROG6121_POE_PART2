namespace RaceDay.Api.DTOs;

/// <summary>Request body to register a new user. Role must be "Organiser" or "Participant".</summary>
public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string Role);

/// <summary>Request body to log in with an existing account.</summary>
public record LoginRequest(string Email, string Password);

/// <summary>Returned after a successful register or login.</summary>
public record AuthResponse(int UserId, string FirstName, string LastName, string Email, string Role);
