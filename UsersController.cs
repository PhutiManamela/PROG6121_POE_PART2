using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
[RequireSession]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Views a user's own profile. A user may only view their own profile.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(int id)
    {
        var sessionUserId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (sessionUserId != id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only view your own profile." });
        }

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id);
        if (user is null || user.Role is null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(new UserProfileResponse(user.UserId, user.FirstName, user.LastName, user.Email, user.Role.RoleName, user.CreatedAt));
    }

    /// <summary>Edits the logged-in user's own profile.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateProfileRequest request)
    {
        var sessionUserId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (sessionUserId != id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only edit your own profile." });
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "FirstName, LastName and Email are all required." });
        }

        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id);
        if (user is null || user.Role is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var emailTaken = await _db.Users.AnyAsync(u => u.UserId != id && u.Email.ToLower() == request.Email.ToLower());
        if (emailTaken)
        {
            return BadRequest(new { message = "That email is already in use by another account." });
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = request.Email.Trim();
        await _db.SaveChangesAsync();

        return Ok(new UserProfileResponse(user.UserId, user.FirstName, user.LastName, user.Email, user.Role.RoleName, user.CreatedAt));
    }
}
