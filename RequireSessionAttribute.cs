using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Services;

namespace RaceDay.Api.Authorization;

/// <summary>
/// Action filter that enforces session-based authentication and, optionally, role-based access.
/// Apply with no arguments to require any logged-in user ("Any" in the endpoint plan), or with one or
/// more role names (e.g. [RequireSession("Organiser")]) to restrict the action to those roles.
/// Returns 401 Unauthorized if no session is present, 403 Forbidden if the session's role is not allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RequireSessionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string[] _allowedRoles;

    public RequireSessionAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);

        if (userId is null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "You must be logged in to access this resource." });
            return;
        }

        if (_allowedRoles.Length > 0)
        {
            var role = session.GetString(SessionKeys.Role);
            if (role is null || !_allowedRoles.Contains(role))
            {
                context.Result = new ObjectResult(new
                {
                    message = $"This action requires one of the following roles: {string.Join(", ", _allowedRoles)}."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }

        await next();
    }
}
