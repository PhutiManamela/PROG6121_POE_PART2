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
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Lists all race categories for an event (e.g. 5km, 10km, 21km, Marathon). Open to anyone.</summary>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var eventExists = await _db.Events.AnyAsync(e => e.EventId == eventId);
        if (!eventExists)
        {
            return NotFound(new { message = "Event not found." });
        }

        var categories = await _db.Categories
            .Where(c => c.EventId == eventId)
            .Select(c => new CategoryResponse(c.CategoryId, c.EventId, c.Name, c.DistanceKm, c.MaxParticipants, c.EntryFee))
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>Adds a new category to an event owned by the logged-in organiser.</summary>
    [HttpPost("api/events/{eventId:int}/categories")]
    [RequireSession("Organiser")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, [FromBody] CreateCategoryRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
        {
            return NotFound(new { message = "Event not found." });
        }

        var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        if (ev.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only add categories to events you organise." });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.DistanceKm <= 0 || request.MaxParticipants <= 0)
        {
            return BadRequest(new { message = "Name, a positive DistanceKm and a positive MaxParticipants are required." });
        }

        var category = new Category
        {
            EventId = eventId,
            Name = request.Name.Trim(),
            DistanceKm = request.DistanceKm,
            MaxParticipants = request.MaxParticipants,
            EntryFee = request.EntryFee
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var response = new CategoryResponse(category.CategoryId, category.EventId, category.Name, category.DistanceKm, category.MaxParticipants, category.EntryFee);
        return CreatedAtAction(nameof(GetForEvent), new { eventId }, response);
    }

    /// <summary>Updates a category on an event owned by the logged-in organiser.</summary>
    [HttpPut("api/categories/{id:int}")]
    [RequireSession("Organiser")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null || category.Event is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        if (category.Event.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only edit categories on events you organise." });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.DistanceKm <= 0 || request.MaxParticipants <= 0)
        {
            return BadRequest(new { message = "Name, a positive DistanceKm and a positive MaxParticipants are required." });
        }

        category.Name = request.Name.Trim();
        category.DistanceKm = request.DistanceKm;
        category.MaxParticipants = request.MaxParticipants;
        category.EntryFee = request.EntryFee;
        await _db.SaveChangesAsync();

        return Ok(new CategoryResponse(category.CategoryId, category.EventId, category.Name, category.DistanceKm, category.MaxParticipants, category.EntryFee));
    }

    /// <summary>Removes a category from an event owned by the logged-in organiser. Fails if the category has enrolments.</summary>
    [HttpDelete("api/categories/{id:int}")]
    [RequireSession("Organiser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.Enrolments)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null || category.Event is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        if (category.Event.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only delete categories on events you organise." });
        }

        if (category.Enrolments.Any())
        {
            return Conflict(new { message = "This category has enrolments and cannot be deleted." });
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
