using RaceDay.Api.Models;

namespace RaceDay.Api.Services;

public interface IJwtTokenService
{
    /// <summary>Creates a signed JWT carrying the user's id and role.</summary>
    string CreateToken(User user, string role);

    /// <summary>Validates a JWT and extracts the user id and role. Returns false if it is invalid or expired.</summary>
    bool TryValidate(string token, out int userId, out string role);
}
