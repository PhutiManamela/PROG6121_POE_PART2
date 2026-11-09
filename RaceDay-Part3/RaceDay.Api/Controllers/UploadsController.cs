using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Image uploads. The MVC app never talks to Azure directly: it forwards the file here as multipart/form-data,
/// this API stores it in Azure Blob Storage and persists the resulting URL in SQL Server.
/// </summary>
[ApiController]
public class UploadsController : ControllerBase
{
    private const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedTypes = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;

    public UploadsController(RaceDayDbContext db, IBlobStorageService blobs)
    {
        _db = db;
        _blobs = blobs;
    }

    /// <summary>Uploads a generic image (JPEG, PNG, WebP or GIF, max 5 MB) to Azure Blob Storage and returns its URL.</summary>
    [HttpPost("api/upload")]
    [RequireSession]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file)
    {
        var (url, error) = await StoreAsync(file, "uploads");
        return error ?? Ok(new UploadResponse(url!));
    }

    /// <summary>Uploads a banner image for an event owned by the logged-in organiser and saves its URL on the event.</summary>
    [HttpPost("api/events/{eventId:int}/banner")]
    [RequireSession("Organiser")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadBanner(int eventId, [FromForm] IFormFile file)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
        {
            return NotFound(new { message = "Event not found." });
        }

        if (ev.OrganiserId != HttpContext.GetUserId()!.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only upload banners for events you organise." });
        }

        var (url, error) = await StoreAsync(file, "banners");
        if (error is not null)
        {
            return error;
        }

        ev.BannerUrl = url;
        await _db.SaveChangesAsync();

        return Ok(new UploadResponse(url!));
    }

    /// <summary>Uploads the logged-in user's profile picture and saves its URL on their profile.</summary>
    [HttpPost("api/users/{id:int}/profile-picture")]
    [RequireSession]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadProfilePicture(int id, [FromForm] IFormFile file)
    {
        if (HttpContext.GetUserId() != id)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only change your own profile picture." });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        var (url, error) = await StoreAsync(file, "profiles");
        if (error is not null)
        {
            return error;
        }

        user.ProfilePictureUrl = url;
        await _db.SaveChangesAsync();

        return Ok(new UploadResponse(url!));
    }

    private async Task<(string? Url, IActionResult? Error)> StoreAsync(IFormFile file, string folder)
    {
        if (file is null || file.Length == 0)
        {
            return (null, BadRequest(new { message = "Please choose a file to upload." }));
        }

        if (file.Length > MaxFileBytes)
        {
            return (null, BadRequest(new { message = "The image must be 5 MB or smaller." }));
        }

        if (!AllowedTypes.TryGetValue(file.ContentType ?? string.Empty, out var extension))
        {
            return (null, BadRequest(new { message = "Only JPEG, PNG, WebP or GIF images are allowed." }));
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var url = await _blobs.UploadAsync(stream, $"{folder}/{Guid.NewGuid():N}{extension}", file.ContentType!);
            return (url, null);
        }
        catch (InvalidOperationException ex)
        {
            return (null, StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message }));
        }
    }
}
