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
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Browses all events. Supports optional "search" (name/location) and "date" query filters. Open to anyone.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] DateTime? date)
    {
        var query = _db.Events.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e => e.Name.Contains(search) || e.Location.Contains(search));
        }

        if (date.HasValue)
        {
            query = query.Where(e => e.EventDate.Date == date.Value.Date);
        }

        var events = await query
            .OrderBy(e => e.EventDate)
            .Select(e => new EventSummaryResponse(e.EventId, e.Name, e.EventDate, e.Location, e.OrganiserId))
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>Views full detail of a single event, including its categories. Open to anyone.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev is null || ev.Organiser is null)
        {
            return NotFound(new { message = "Event not found." });
        }

        return Ok(MapToEventResponse(ev));
    }

    /// <summary>Creates a new event owned by the logged-in organiser. Only Organisers may create events.</summary>
    [HttpPost]
    [RequireSession("Organiser")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Location))
        {
            return BadRequest(new { message = "Name and Location are required." });
        }

        var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

        var ev = new Event
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            EventDate = request.EventDate,
            Location = request.Location.Trim(),
            OrganiserId = organiserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        ev = await _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .FirstAsync(e => e.EventId == ev.EventId);

        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, MapToEventResponse(ev));
    }

    /// <summary>Updates an event owned by the logged-in organiser.</summary>
    [HttpPut("{id:int}")]
    [RequireSession("Organiser")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEventRequest request)
    {
        var ev = await _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev is null)
        {
            return NotFound(new { message = "Event not found." });
        }

        var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        if (ev.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only edit events you organise." });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Location))
        {
            return BadRequest(new { message = "Name and Location are required." });
        }

        ev.Name = request.Name.Trim();
        ev.Description = request.Description;
        ev.EventDate = request.EventDate;
        ev.Location = request.Location.Trim();
        await _db.SaveChangesAsync();

        return Ok(MapToEventResponse(ev));
    }

    /// <summary>Deletes an event owned by the logged-in organiser. Fails if the event still has enrolments.</summary>
    [HttpDelete("{id:int}")]
    [RequireSession("Organiser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories)
            .ThenInclude(c => c.Enrolments)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev is null)
        {
            return NotFound(new { message = "Event not found." });
        }

        var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        if (ev.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only delete events you organise." });
        }

        var hasEnrolments = ev.Categories.Any(c => c.Enrolments.Any());
        if (hasEnrolments)
        {
            return Conflict(new { message = "This event has active enrolments and cannot be deleted." });
        }

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static EventResponse MapToEventResponse(Event ev) => new(
        ev.EventId,
        ev.Name,
        ev.Description,
        ev.EventDate,
        ev.Location,
        ev.OrganiserId,
        ev.Organiser is null ? string.Empty : $"{ev.Organiser.FirstName} {ev.Organiser.LastName}",
        ev.Categories
            .Select(c => new CategoryResponse(c.CategoryId, c.EventId, c.Name, c.DistanceKm, c.MaxParticipants, c.EntryFee))
            .ToList()
    );
}
