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
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Enters the logged-in participant into a category. Records the link between the participant, event and category.</summary>
    [HttpPost("api/categories/{categoryId:int}/enrolments")]
    [RequireSession("Participant")]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int categoryId)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.Enrolments)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

        if (category is null || category.Event is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        var participantId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

        var alreadyEnrolled = category.Enrolments.Any(en => en.ParticipantId == participantId && en.Status == "Confirmed");
        if (alreadyEnrolled)
        {
            return Conflict(new { message = "You are already enrolled in this category." });
        }

        var confirmedCount = category.Enrolments.Count(en => en.Status == "Confirmed");
        if (confirmedCount >= category.MaxParticipants)
        {
            return Conflict(new { message = "This category is full." });
        }

        var enrolment = new Enrolment
        {
            ParticipantId = participantId,
            CategoryId = categoryId,
            EnrolmentDate = DateTime.UtcNow,
            Status = "Confirmed"
        };

        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var participant = await _db.Users.FirstAsync(u => u.UserId == participantId);

        var response = new EnrolmentResponse(
            enrolment.EnrolmentId,
            participant.UserId,
            $"{participant.FirstName} {participant.LastName}",
            category.CategoryId,
            category.Name,
            category.Event.EventId,
            category.Event.Name,
            enrolment.EnrolmentDate,
            enrolment.Status);

        return CreatedAtAction(nameof(GetMyEnrolments), null, response);
    }

    /// <summary>Views the logged-in participant's own enrolments.</summary>
    [HttpGet("api/enrolments/me")]
    [RequireSession("Participant")]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyEnrolments()
    {
        var participantId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

        var enrolments = await _db.Enrolments
            .Include(en => en.Participant)
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .Where(en => en.ParticipantId == participantId)
            .Select(en => new EnrolmentResponse(
                en.EnrolmentId,
                en.ParticipantId,
                en.Participant!.FirstName + " " + en.Participant.LastName,
                en.CategoryId,
                en.Category!.Name,
                en.Category.EventId,
                en.Category.Event!.Name,
                en.EnrolmentDate,
                en.Status))
            .ToListAsync();

        return Ok(enrolments);
    }

    /// <summary>Views all participant enrolments for an event owned by the logged-in organiser.</summary>
    [HttpGet("api/events/{eventId:int}/participants")]
    [RequireSession("Organiser")]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventParticipants(int eventId)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
        {
            return NotFound(new { message = "Event not found." });
        }

        var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        if (ev.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only view enrolments for events you organise." });
        }

        var enrolments = await _db.Enrolments
            .Include(en => en.Participant)
            .Include(en => en.Category)
            .Where(en => en.Category!.EventId == eventId)
            .Select(en => new EnrolmentResponse(
                en.EnrolmentId,
                en.ParticipantId,
                en.Participant!.FirstName + " " + en.Participant.LastName,
                en.CategoryId,
                en.Category!.Name,
                eventId,
                ev.Name,
                en.EnrolmentDate,
                en.Status))
            .ToListAsync();

        return Ok(enrolments);
    }

    /// <summary>Cancels the logged-in participant's own enrolment.</summary>
    [HttpDelete("api/enrolments/{id:int}")]
    [RequireSession("Participant")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await _db.Enrolments.FirstOrDefaultAsync(en => en.EnrolmentId == id);
        if (enrolment is null)
        {
            return NotFound(new { message = "Enrolment not found." });
        }

        var participantId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
        if (enrolment.ParticipantId != participantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only cancel your own enrolment." });
        }

        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
