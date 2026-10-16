using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher _hasher;

    public AuthController(RaceDayDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>Registers a new user as either an Organiser or a Participant.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "FirstName, LastName, Email and Password are all required." });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters long." });
        }

        if (request.Role != "Organiser" && request.Role != "Participant")
        {
            return BadRequest(new { message = "Role must be either 'Organiser' or 'Participant'." });
        }

        var emailExists = await _db.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());
        if (emailExists)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == request.Role);
        if (role is null)
        {
            return BadRequest(new { message = "Invalid role specified." });
        }

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
            RoleId = role.RoleId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var response = new AuthResponse(user.UserId, user.FirstName, user.LastName, user.Email, role.RoleName);
        return CreatedAtAction(nameof(UsersController.GetProfile), "Users", new { id = user.UserId }, response);
    }

    /// <summary>Authenticates a user and starts a session that tracks their identity and role for subsequent requests.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and Password are required." });
        }

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (user is null || user.Role is null || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.RoleName);
        HttpContext.Session.SetString(SessionKeys.FullName, $"{user.FirstName} {user.LastName}");

        return Ok(new AuthResponse(user.UserId, user.FirstName, user.LastName, user.Email, user.Role.RoleName));
    }

    /// <summary>Ends the current session.</summary>
    [HttpPost("logout")]
    [RequireSession]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }
}
