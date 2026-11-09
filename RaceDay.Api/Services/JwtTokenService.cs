using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RaceDay.Api.Models;

namespace RaceDay.Api.Services;

/// <summary>
/// Issues and validates the JWT bearer tokens used by the Part 3 MVC client.
/// Browser/Swagger callers keep using the Part 2 session cookie; server-side clients send "Authorization: Bearer {token}".
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly SymmetricSecurityKey _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        var secret = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be configured and at least 32 characters long.");
        }

        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _issuer = configuration["Jwt:Issuer"] ?? "RaceDay.Api";
        _audience = configuration["Jwt:Audience"] ?? "RaceDay.Clients";
        _expiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 120;
    }

    public string CreateToken(User user, string role)
    {
        var claims = new[]
        {
            new Claim("uid", user.UserId.ToString()),
            new Claim("role", role),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool TryValidate(string token, out int userId, out string role)
    {
        userId = 0;
        role = string.Empty;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.InboundClaimTypeMap.Clear();

            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            var uid = principal.FindFirst("uid")?.Value;
            var tokenRole = principal.FindFirst("role")?.Value;

            if (!int.TryParse(uid, out userId) || string.IsNullOrEmpty(tokenRole))
            {
                return false;
            }

            role = tokenRole;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
