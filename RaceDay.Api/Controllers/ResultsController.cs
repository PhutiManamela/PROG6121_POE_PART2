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
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Captures the finish time and position for an enrolment. Only the organiser of that event may record it.</summary>
    [HttpPost("api/enrolments/{enrolmentId:int}/result")]
    [RequireSession("Organiser")]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Capture(int enrolmentId, [FromBody] CaptureResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(en => en.Participant)
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .Include(en => en.Result)
            .FirstOrDefaultAsync(en => en.EnrolmentId == enrolmentId);

        if (enrolment is null || enrolment.Category?.Event is null)
        {
            return NotFound(new { message = "Enrolment not found." });
        }

        var organiserId = HttpContext.GetUserId()!.Value;
        if (enrolment.Category.Event.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only capture results for events you organise." });
        }

        if (enrolment.Result is not null)
        {
            return Conflict(new { message = "A result has already been recorded for this enrolment." });
        }

        if (request.FinishTime <= TimeSpan.Zero)
        {
            return BadRequest(new { message = "FinishTime must be a positive duration." });
        }

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = request.FinishTime,
            Position = request.Position,
            RecordedByUserId = organiserId,
            RecordedAt = DateTime.UtcNow
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var total = await CountFinishersAsync(enrolment.CategoryId);
        return CreatedAtAction(nameof(GetMyResults), null, MapToResultResponse(result, enrolment, total));
    }

    /// <summary>Corrects a previously captured result. Only the organiser who owns the event may edit it.</summary>
    [HttpPut("api/results/{id:int}")]
    [RequireSession("Organiser")]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] CaptureResultRequest request)
    {
        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(en => en!.Participant)
            .Include(r => r.Enrolment).ThenInclude(en => en!.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id);

        if (result is null || result.Enrolment?.Category?.Event is null)
        {
            return NotFound(new { message = "Result not found." });
        }

        var organiserId = HttpContext.GetUserId()!.Value;
        if (result.Enrolment.Category.Event.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only edit results for events you organise." });
        }

        if (request.FinishTime <= TimeSpan.Zero)
        {
            return BadRequest(new { message = "FinishTime must be a positive duration." });
        }

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        await _db.SaveChangesAsync();

        var total = await CountFinishersAsync(result.Enrolment.CategoryId);
        return Ok(MapToResultResponse(result, result.Enrolment, total));
    }

    /// <summary>Views the logged-in participant's own race results, including how many finishers were in each category.</summary>
    [HttpGet("api/results/me")]
    [RequireSession("Participant")]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyResults()
    {
        var participantId = HttpContext.GetUserId()!.Value;

        var results = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(en => en!.Participant)
            .Include(r => r.Enrolment).ThenInclude(en => en!.Category).ThenInclude(c => c!.Event)
            .Where(r => r.Enrolment!.ParticipantId == participantId)
            .ToListAsync();

        var categoryIds = results.Select(r => r.Enrolment!.CategoryId).Distinct().ToList();
        var totals = await _db.Results
            .Where(r => categoryIds.Contains(r.Enrolment!.CategoryId))
            .GroupBy(r => r.Enrolment!.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

        return Ok(results
            .Select(r => MapToResultResponse(r, r.Enrolment!, totals.GetValueOrDefault(r.Enrolment!.CategoryId)))
            .ToList());
    }

    /// <summary>Views the public leaderboard for a category, ordered by finishing position then finish time. Open to anyone.</summary>
    [HttpGet("api/categories/{categoryId:int}/leaderboard")]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLeaderboard(int categoryId)
    {
        var categoryExists = await _db.Categories.AnyAsync(c => c.CategoryId == categoryId);
        if (!categoryExists)
        {
            return NotFound(new { message = "Category not found." });
        }

        var results = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(en => en!.Participant)
            .Include(r => r.Enrolment).ThenInclude(en => en!.Category).ThenInclude(c => c!.Event)
            .Where(r => r.Enrolment!.CategoryId == categoryId)
            .OrderBy(r => r.Position ?? int.MaxValue)
            .ThenBy(r => r.FinishTime)
            .ToListAsync();

        return Ok(results.Select(r => MapToResultResponse(r, r.Enrolment!, results.Count)).ToList());
    }

    private Task<int> CountFinishersAsync(int categoryId) =>
        _db.Results.CountAsync(r => r.Enrolment!.CategoryId == categoryId);

    private static ResultResponse MapToResultResponse(Result result, Enrolment enrolment, int totalFinishers) => new(
        result.ResultId,
        enrolment.EnrolmentId,
        enrolment.ParticipantId,
        enrolment.Participant is null ? string.Empty : $"{enrolment.Participant.FirstName} {enrolment.Participant.LastName}",
        enrolment.CategoryId,
        enrolment.Category?.Name ?? string.Empty,
        enrolment.Category?.EventId ?? 0,
        enrolment.Category?.Event?.Name ?? string.Empty,
        result.FinishTime,
        result.Position,
        totalFinishers,
        result.RecordedAt
    );
}
