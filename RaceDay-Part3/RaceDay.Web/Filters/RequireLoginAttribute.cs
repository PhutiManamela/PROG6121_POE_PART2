using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Helpers;

namespace RaceDay.Web.Filters;

/// <summary>
/// Route protection for the MVC app. Anyone without a logged-in session is redirected to the login screen
/// (and returned to the page they wanted afterwards); a logged-in user of the wrong role is sent to "Access denied".
/// Usage: [RequireLogin] for any user, [RequireLogin("Organiser")] or [RequireLogin("Participant")] for one role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public class RequireLoginAttribute : Attribute, IActionFilter
{
    private readonly string[] _roles;

    public RequireLoginAttribute(params string[] roles)
    {
        _roles = roles;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var session = context.HttpContext.Session;

        if (string.IsNullOrEmpty(session.GetString(SessionKeys.Token)))
        {
            var request = context.HttpContext.Request;
            var returnUrl = request.Method == HttpMethods.Get ? request.Path.Value + request.QueryString.Value : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(session.GetString(SessionKeys.Role)))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Home", null);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
