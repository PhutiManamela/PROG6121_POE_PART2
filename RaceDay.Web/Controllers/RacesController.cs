using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

/// <summary>Public race browsing, plus one-click enrolment for logged-in participants.</summary>
public class RacesController : AppController
{
    private readonly IRaceDayApiClient _api;

    public RacesController(IRaceDayApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index(string? search, DateTime? date, string? eventType)
    {
        var result = await _api.GetEventsAsync(search, date);
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
        }

        var events = result.Data ?? new List<EventSummary>();
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            events = events.Where(e => e.EventType == eventType).ToList();
        }

        return View(new RaceCatalogViewModel
        {
            Events = events,
            Search = search,
            Date = date,
            EventType = eventType
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var result = await _api.GetEventAsync(id);
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error ?? "We couldn't find that race.";
            return RedirectToAction(nameof(Index));
        }

        var model = new RaceDetailsViewModel { Event = result.Data };

        if (CurrentRole == "Participant")
        {
            var mine = await _api.GetMyEnrolmentsAsync();
            if (mine.Success && mine.Data is not null)
            {
                model.EnrolledCategoryIds = mine.Data
                    .Where(e => e.Status == "Confirmed")
                    .Select(e => e.CategoryId)
                    .ToHashSet();
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireLogin("Participant")]
    public async Task<IActionResult> Enrol(int categoryId, int eventId)
    {
        var result = await _api.EnrolAsync(categoryId);

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (result.Success)
        {
            TempData["Success"] = "You're in! You've been enrolled in this category.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Details), new { id = eventId });
    }

    public async Task<IActionResult> Leaderboard(int categoryId, int eventId)
    {
        var result = await _api.GetLeaderboardAsync(categoryId);
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id = eventId });
        }

        var results = result.Data ?? new List<ResultItem>();
        var first = results.FirstOrDefault();

        return View(new LeaderboardViewModel
        {
            EventId = eventId,
            CategoryId = categoryId,
            EventName = first?.EventName ?? string.Empty,
            CategoryName = first?.CategoryName ?? string.Empty,
            Results = results
        });
    }
}
