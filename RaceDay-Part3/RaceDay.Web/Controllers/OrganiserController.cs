using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

/// <summary>Organiser workflows: dashboard, events, categories, participant rosters and results capture.</summary>
[RequireLogin("Organiser")]
public class OrganiserController : AppController
{
    private static readonly Regex FinishTimePattern = new(@"^\d{1,2}:[0-5]\d:[0-5]\d$", RegexOptions.Compiled);

    private readonly IRaceDayApiClient _api;

    public OrganiserController(IRaceDayApiClient api)
    {
        _api = api;
    }

    // ------------------------------------------------------------------ Dashboard + events list

    public async Task<IActionResult> Dashboard()
    {
        var (rows, failure) = await LoadMyEventRowsAsync();
        if (failure is not null)
        {
            return failure;
        }

        var today = DateTime.Today;
        return View(new DashboardViewModel
        {
            HostedEvents = rows.Count,
            UpcomingEvents = rows.Count(r => r.Event.EventDate.Date >= today),
            TotalParticipants = rows.Sum(r => r.Participants),
            NextEvent = rows.Select(r => r.Event).Where(e => e.EventDate.Date >= today).OrderBy(e => e.EventDate).FirstOrDefault(),
            Rows = rows
        });
    }

    public async Task<IActionResult> Events()
    {
        var (rows, failure) = await LoadMyEventRowsAsync();
        return failure ?? View(rows);
    }

    // ------------------------------------------------------------------ Event CRUD

    [HttpGet]
    public IActionResult CreateEvent() =>
        View("EventForm", new EventFormViewModel { EventDate = DateTime.Today.AddMonths(1) });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        if (model.EventDate is { } date && date.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(model.EventDate), "The event date can't be in the past.");
        }

        if (!ModelState.IsValid)
        {
            return View("EventForm", model);
        }

        var result = await _api.CreateEventAsync(ToRequest(model));

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't create the event.");
            return View("EventForm", model);
        }

        var bannerNote = await UploadBannerIfProvidedAsync(result.Data.EventId, model.Banner);
        TempData["Success"] = "Event created. Now add some categories so participants can enter.";
        if (bannerNote is not null)
        {
            TempData["Warning"] = bannerNote;
        }

        return RedirectToAction(nameof(Categories), new { eventId = result.Data.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var result = await _api.GetEventAsync(id);

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error ?? "We couldn't find that event.";
            return RedirectToAction(nameof(Events));
        }

        if (result.Data.OrganiserId != CurrentUserId)
        {
            TempData["Error"] = "You can only edit events you organise.";
            return RedirectToAction(nameof(Events));
        }

        var e = result.Data;
        return View("EventForm", new EventFormViewModel
        {
            EventId = e.EventId,
            Name = e.Name,
            Description = e.Description,
            EventDate = e.EventDate,
            Location = e.Location,
            DistanceKm = e.DistanceKm,
            EventType = e.EventType,
            BannerUrl = e.BannerUrl
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.EventId = id;

        if (!ModelState.IsValid)
        {
            return View("EventForm", model);
        }

        var result = await _api.UpdateEventAsync(id, ToRequest(model));

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't update the event.");
            model.BannerUrl = model.BannerUrl ?? result.Data?.BannerUrl;
            return View("EventForm", model);
        }

        var bannerNote = await UploadBannerIfProvidedAsync(id, model.Banner);
        TempData["Success"] = "Event updated.";
        if (bannerNote is not null)
        {
            TempData["Warning"] = bannerNote;
        }

        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await _api.DeleteEventAsync(id);

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (result.Success)
        {
            TempData["Success"] = "Event deleted.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Events));
    }

    // ------------------------------------------------------------------ Categories

    public async Task<IActionResult> Categories(int eventId)
    {
        var result = await _api.GetEventAsync(eventId);

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error ?? "We couldn't find that event.";
            return RedirectToAction(nameof(Events));
        }

        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> CreateCategory(int eventId)
    {
        var ev = await _api.GetEventAsync(eventId);

        if (RedirectIfSessionExpired(ev) is { } expired)
        {
            return expired;
        }

        if (!ev.Success || ev.Data is null)
        {
            TempData["Error"] = ev.Error ?? "We couldn't find that event.";
            return RedirectToAction(nameof(Events));
        }

        return View("CategoryForm", new CategoryFormViewModel
        {
            EventId = eventId,
            EventName = ev.Data.Name,
            MaxParticipants = 100,
            EntryFee = 0
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("CategoryForm", model);
        }

        var result = await _api.CreateCategoryAsync(model.EventId, ToRequest(model));

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't add the category.");
            return View("CategoryForm", model);
        }

        TempData["Success"] = "Category added.";
        return RedirectToAction(nameof(Categories), new { eventId = model.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id, int eventId)
    {
        var ev = await _api.GetEventAsync(eventId);

        if (RedirectIfSessionExpired(ev) is { } expired)
        {
            return expired;
        }

        var category = ev.Data?.Categories.FirstOrDefault(c => c.CategoryId == id);
        if (!ev.Success || ev.Data is null || category is null)
        {
            TempData["Error"] = ev.Error ?? "We couldn't find that category.";
            return RedirectToAction(nameof(Events));
        }

        return View("CategoryForm", new CategoryFormViewModel
        {
            CategoryId = category.CategoryId,
            EventId = eventId,
            EventName = ev.Data.Name,
            Name = category.Name,
            DistanceKm = category.DistanceKm,
            MaxParticipants = category.MaxParticipants,
            EntryFee = category.EntryFee
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, CategoryFormViewModel model)
    {
        model.CategoryId = id;

        if (!ModelState.IsValid)
        {
            return View("CategoryForm", model);
        }

        var result = await _api.UpdateCategoryAsync(id, ToRequest(model));

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't update the category.");
            return View("CategoryForm", model);
        }

        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Categories), new { eventId = model.EventId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, int eventId)
    {
        var result = await _api.DeleteCategoryAsync(id);

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (result.Success)
        {
            TempData["Success"] = "Category removed.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Categories), new { eventId });
    }

    // ------------------------------------------------------------------ Participant rosters

    public async Task<IActionResult> Participants(int eventId, int? categoryId)
    {
        var ev = await _api.GetEventAsync(eventId);

        if (RedirectIfSessionExpired(ev) is { } expired)
        {
            return expired;
        }

        if (!ev.Success || ev.Data is null)
        {
            TempData["Error"] = ev.Error ?? "We couldn't find that event.";
            return RedirectToAction(nameof(Events));
        }

        var enrolments = await _api.GetEventParticipantsAsync(eventId);
        if (!enrolments.Success)
        {
            TempData["Error"] = enrolments.Error;
        }

        var list = enrolments.Data ?? new List<EnrolmentItem>();
        if (categoryId.HasValue)
        {
            list = list.Where(e => e.CategoryId == categoryId.Value).ToList();
        }

        return View(new ParticipantListViewModel { Event = ev.Data, Enrolments = list, CategoryId = categoryId });
    }

    // ------------------------------------------------------------------ Results capture

    public async Task<IActionResult> Results(int eventId)
    {
        var ev = await _api.GetEventAsync(eventId);

        if (RedirectIfSessionExpired(ev) is { } expired)
        {
            return expired;
        }

        if (!ev.Success || ev.Data is null)
        {
            TempData["Error"] = ev.Error ?? "We couldn't find that event.";
            return RedirectToAction(nameof(Events));
        }

        var enrolments = await _api.GetEventParticipantsAsync(eventId);
        if (!enrolments.Success)
        {
            TempData["Error"] = enrolments.Error;
        }

        var allEnrolments = (enrolments.Data ?? new List<EnrolmentItem>()).Where(e => e.Status == "Confirmed").ToList();

        // The leaderboard holds the existing results (and their ids, needed to correct a result).
        var leaderboards = await Task.WhenAll(ev.Data.Categories.Select(c => _api.GetLeaderboardAsync(c.CategoryId)));

        var model = new ResultsCaptureViewModel { Event = ev.Data };
        for (var i = 0; i < ev.Data.Categories.Count; i++)
        {
            var category = ev.Data.Categories[i];
            var existing = leaderboards[i].Data ?? new List<ResultItem>();

            model.Categories.Add(new CategoryResults
            {
                Category = category,
                Rows = allEnrolments
                    .Where(e => e.CategoryId == category.CategoryId)
                    .Select(e => new ResultRow
                    {
                        Enrolment = e,
                        Result = existing.FirstOrDefault(r => r.EnrolmentId == e.EnrolmentId)
                    })
                    .ToList()
            });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveResult(int eventId, int enrolmentId, int? resultId, string? finishTime, int? position)
    {
        if (string.IsNullOrWhiteSpace(finishTime) || !FinishTimePattern.IsMatch(finishTime.Trim()))
        {
            TempData["Error"] = "Enter the finish time as HH:MM:SS, for example 00:52:14.";
            return RedirectToAction(nameof(Results), new { eventId });
        }

        if (position is < 1)
        {
            TempData["Error"] = "The finishing position must be 1 or higher.";
            return RedirectToAction(nameof(Results), new { eventId });
        }

        var parts = finishTime.Trim().Split(':');
        var time = new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
        if (time <= TimeSpan.Zero)
        {
            TempData["Error"] = "The finish time must be greater than 00:00:00.";
            return RedirectToAction(nameof(Results), new { eventId });
        }

        var request = new ResultRequest(time, position);
        ApiResult result = resultId.HasValue
            ? await _api.UpdateResultAsync(resultId.Value, request)
            : await _api.CaptureResultAsync(enrolmentId, request);

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (result.Success)
        {
            TempData["Success"] = resultId.HasValue ? "Result updated." : "Result captured.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Results), new { eventId });
    }

    // ------------------------------------------------------------------ Helpers

    /// <summary>Loads the logged-in organiser's events together with their participant counts.</summary>
    private async Task<(List<EventRow> Rows, IActionResult? Failure)> LoadMyEventRowsAsync()
    {
        var events = await _api.GetEventsAsync();

        if (RedirectIfSessionExpired(events) is { } expired)
        {
            return (new List<EventRow>(), expired);
        }

        if (!events.Success)
        {
            TempData["Error"] = events.Error;
        }

        var mine = (events.Data ?? new List<EventSummary>())
            .Where(e => e.OrganiserId == CurrentUserId)
            .OrderBy(e => e.EventDate)
            .ToList();

        var counts = await Task.WhenAll(mine.Select(e => _api.GetEventParticipantsAsync(e.EventId)));

        var rows = mine
            .Select((e, i) => new EventRow
            {
                Event = e,
                Participants = (counts[i].Data ?? new List<EnrolmentItem>()).Count(p => p.Status == "Confirmed")
            })
            .ToList();

        return (rows, null);
    }

    /// <summary>Forwards a chosen banner image to the API. Returns a warning message if that failed, otherwise null.</summary>
    private async Task<string?> UploadBannerIfProvidedAsync(int eventId, IFormFile? banner)
    {
        if (banner is null || banner.Length == 0)
        {
            return null;
        }

        var upload = await _api.UploadBannerAsync(eventId, banner);
        return upload.Success ? null : $"The event was saved, but the banner wasn't uploaded: {upload.Error}";
    }

    private static EventRequest ToRequest(EventFormViewModel m) =>
        new(m.Name.Trim(), m.Description, m.EventDate!.Value, m.Location.Trim(), m.DistanceKm!.Value, m.EventType);

    private static CategoryRequest ToRequest(CategoryFormViewModel m) =>
        new(m.Name.Trim(), m.DistanceKm!.Value, m.MaxParticipants!.Value, m.EntryFee!.Value);
}
