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
            .Select(e => new EventSummaryResponse(e.EventId, e.Name, e.EventDate, e.Location, e.DistanceKm, e.EventType, e.BannerUrl, e.OrganiserId))
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
        var validationError = ValidateEvent(request.Name, request.Location, request.DistanceKm, request.EventType);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var organiserId = HttpContext.GetUserId()!.Value;

        var ev = new Event
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            EventDate = request.EventDate,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = request.EventType,
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

        var organiserId = HttpContext.GetUserId()!.Value;
        if (ev.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only edit events you organise." });
        }

        var validationError = ValidateEvent(request.Name, request.Location, request.DistanceKm, request.EventType);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        ev.Name = request.Name.Trim();
        ev.Description = request.Description;
        ev.EventDate = request.EventDate;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = request.EventType;
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

        var organiserId = HttpContext.GetUserId()!.Value;
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

    private static readonly string[] ValidEventTypes = { "Run", "Walk", "Cycle" };

    private static string? ValidateEvent(string name, string location, decimal distanceKm, string eventType)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(location))
        {
            return "Name and Location are required.";
        }

        if (distanceKm <= 0)
        {
            return "DistanceKm must be greater than zero.";
        }

        if (!ValidEventTypes.Contains(eventType))
        {
            return "EventType must be 'Run', 'Walk' or 'Cycle'.";
        }

        return null;
    }

    private static EventResponse MapToEventResponse(Event ev) => new(
        ev.EventId,
        ev.Name,
        ev.Description,
        ev.EventDate,
        ev.Location,
        ev.DistanceKm,
        ev.EventType,
        ev.BannerUrl,
        ev.OrganiserId,
        ev.Organiser is null ? string.Empty : $"{ev.Organiser.FirstName} {ev.Organiser.LastName}",
        ev.Categories
            .Select(c => new CategoryResponse(c.CategoryId, c.EventId, c.Name, c.DistanceKm, c.MaxParticipants, c.EntryFee))
            .ToList()
    );
}
