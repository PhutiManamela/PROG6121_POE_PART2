using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

public class AccountController : AppController
{
    private readonly IRaceDayApiClient _api;

    public AccountController(IRaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(HttpContext.Session.GetString(SessionKeys.Token)))
        {
            return RedirectToHomeFor(CurrentRole ?? "Participant");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _api.LoginAsync(model.Email, model.Password);
        if (!result.Success || result.Data is null || string.IsNullOrEmpty(result.Data.Token))
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't log you in. Please try again.");
            return View(model);
        }

        SignIn(result.Data);
        TempData["Success"] = $"Welcome back, {result.Data.FirstName}!";

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToHomeFor(result.Data.Role);
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var register = await _api.RegisterAsync(new RegisterRequest(
            model.FirstName, model.LastName, model.Email, model.Password, model.Role));

        if (!register.Success)
        {
            ModelState.AddModelError(string.Empty, register.Error ?? "We couldn't create your account.");
            return View(model);
        }

        // Log the new user straight in.
        var login = await _api.LoginAsync(model.Email, model.Password);
        if (!login.Success || login.Data is null)
        {
            TempData["Success"] = "Your account was created. Please log in.";
            return RedirectToAction(nameof(Login));
        }

        SignIn(login.Data);
        TempData["Success"] = $"Welcome to RaceDay, {login.Data.FirstName}!";
        return RedirectToHomeFor(login.Data.Role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        TempData["Success"] = "You've been logged out.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [RequireLogin]
    public async Task<IActionResult> Profile()
    {
        var result = await _api.GetProfileAsync(CurrentUserId);

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error ?? "We couldn't load your profile.";
            return RedirectToAction("Index", "Home");
        }

        return View(ToViewModel(result.Data));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireLogin]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _api.UpdateProfileAsync(CurrentUserId,
            new ProfileUpdateRequest(model.FirstName, model.LastName, model.Email));

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't update your profile.");
            return View(model);
        }

        HttpContext.Session.SetString(SessionKeys.FullName, $"{result.Data.FirstName} {result.Data.LastName}");
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireLogin]
    public async Task<IActionResult> UploadPicture(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Please choose an image to upload.";
            return RedirectToAction(nameof(Profile));
        }

        // The file is forwarded to the API, which stores it in Azure Blob Storage and saves the URL.
        var result = await _api.UploadProfilePictureAsync(CurrentUserId, file);

        if (RedirectIfSessionExpired(result) is { } expired)
        {
            return expired;
        }

        if (result.Success)
        {
            TempData["Success"] = "Your profile picture has been updated.";
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return RedirectToAction(nameof(Profile));
    }

    private static ProfileViewModel ToViewModel(UserProfile profile) => new()
    {
        UserId = profile.UserId,
        FirstName = profile.FirstName,
        LastName = profile.LastName,
        Email = profile.Email,
        Role = profile.Role,
        CreatedAt = profile.CreatedAt,
        ProfilePictureUrl = profile.ProfilePictureUrl
    };
}
