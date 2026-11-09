using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    private readonly IRaceDayApiClient _api;

    public HomeController(IRaceDayApiClient api)
    {
        _api = api;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _api.GetEventsAsync();
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
        }

        var upcoming = (result.Data ?? new List<EventSummary>())
            .Where(e => e.EventDate.Date >= DateTime.Today)
            .OrderBy(e => e.EventDate)
            .Take(3)
            .ToList();

        return View(upcoming);
    }

    public IActionResult AccessDenied() => View();

    public IActionResult Status(int code)
    {
        ViewBag.Code = code;
        ViewBag.Message = code switch
        {
            404 => "We couldn't find that page.",
            403 => "You don't have permission to see that page.",
            _ => "Something went wrong."
        };
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
