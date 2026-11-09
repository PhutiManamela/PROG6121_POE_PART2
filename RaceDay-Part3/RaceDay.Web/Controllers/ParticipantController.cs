using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

/// <summary>Participant workflows: my enrolments and my results.</summary>
[RequireLogin("Participant")]
public class ParticipantController : AppController
{
    private readonly IRaceDayApiClient _api;

    public ParticipantController(IRaceDayApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Enrolments()
    {
        var enrolments = await _api.GetMyEnrolmentsAsync();

        if (RedirectIfSessionExpired(enrolments) is { } expired)
        {
            return expired;
        }

        if (!enrolments.Success)
        {
            TempData["Error"] = enrolments.Error;
        }

        // Enrolments carry the event id but not its date, so look the dates up to split active from past.
        var events = await _api.GetEventsAsync();
        var dates = (events.Data ?? new List<EventSummary>()).ToDictionary(e => e.EventId, e => e.EventDate);

        var rows = (enrolments.Data ?? new List<EnrolmentItem>())
            .Select(e => new EnrolmentRow
            {
                Enrolment = e,
                EventDate = dates.TryGetValue(e.EventId, out var date) ? date : null
            })
            .ToList();

        var today = DateTime.Today;
        var model = new MyEnrolmentsViewModel
        {
            Active = rows
                .Where(r => r.Enrolment.Status != "Cancelled" && (r.EventDate is null || r.EventDate.Value.Date >= today))
                .OrderBy(r => r.EventDate)
                .ToList(),
            Past = rows
                .Where(r => r.Enrolment.Status == "Cancelled" || (r.EventDate is not null && r.EventDate.Value.Date < today))
                .OrderByDescending(r => r.EventDate)
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int enrolmentId)
    {
        var result = await _api.CancelEnrolmentAsync(enrolmentId);

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (result.Success)
        {
            TempData["Success"] = "Your enrolment has been cancelled.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Enrolments));
    }

    public async Task<IActionResult> Results()
    {
        var result = await _api.GetMyResultsAsync();

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
        }

        return View((result.Data ?? new List<ResultItem>()).OrderByDescending(r => r.RecordedAt).ToList());
    }
}
