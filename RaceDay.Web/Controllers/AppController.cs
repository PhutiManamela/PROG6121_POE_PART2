using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Helpers;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Shared helpers for all MVC controllers.</summary>
public abstract class AppController : Controller
{
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);

    /// <summary>
    /// If the API rejected our token (it expired, or the API restarted), end the session and send the user to log in again.
    /// Returns null when the result is not an authentication failure.
    /// </summary>
    protected IActionResult? RedirectIfSessionExpired(ApiResult result)
    {
        if (!result.IsUnauthorized)
        {
            return null;
        }

        HttpContext.Session.Clear();
        TempData["Error"] = "Your session has expired. Please log in again.";
        return RedirectToAction("Login", "Account");
    }

    /// <summary>Starts the logged-in state: stores the JWT, role and name in server-side session.</summary>
    protected void SignIn(Models.AuthResponse auth)
    {
        HttpContext.Session.SetString(SessionKeys.Token, auth.Token ?? string.Empty);
        HttpContext.Session.SetString(SessionKeys.Role, auth.Role);
        HttpContext.Session.SetInt32(SessionKeys.UserId, auth.UserId);
        HttpContext.Session.SetString(SessionKeys.FullName, $"{auth.FirstName} {auth.LastName}");
    }

    protected IActionResult RedirectToHomeFor(string role) =>
        role == "Organiser"
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Enrolments", "Participant");
}
