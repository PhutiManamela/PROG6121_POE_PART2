using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Services;

namespace RaceDay.Api.Authorization;

/// <summary>
/// Action filter that enforces authentication and, optionally, role-based access.
/// A caller is authenticated by EITHER a valid "Authorization: Bearer {jwt}" header (used by the MVC client)
/// OR the server-side session started by POST /api/auth/login (used by Swagger and browsers).
/// Apply with no arguments to require any logged-in user ("Any" in the endpoint plan), or with role names
/// (e.g. [RequireSession("Organiser")]) to restrict the action to those roles.
/// Returns 401 Unauthorized if the caller is not authenticated, 403 Forbidden if their role is not allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RequireSessionAttribute : Attribute, IAsyncActionFilter
{
    private const string BearerPrefix = "Bearer ";
    private readonly string[] _allowedRoles;

    public RequireSessionAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        int? userId = null;
        string? role = null;

        // An explicit bearer token wins over any session cookie.
        var header = http.Request.Headers.Authorization.ToString();
        if (header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var jwt = http.RequestServices.GetRequiredService<IJwtTokenService>();
            if (jwt.TryValidate(header[BearerPrefix.Length..].Trim(), out var tokenUserId, out var tokenRole))
            {
                userId = tokenUserId;
                role = tokenRole;
                http.Items[SessionKeys.UserId] = tokenUserId;
                http.Items[SessionKeys.Role] = tokenRole;
            }
        }

        if (userId is null)
        {
            userId = http.Session.GetInt32(SessionKeys.UserId);
            role = http.Session.GetString(SessionKeys.Role);
        }

        if (userId is null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "You must be logged in to access this resource." });
            return;
        }

        if (_allowedRoles.Length > 0 && (role is null || !_allowedRoles.Contains(role)))
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

        await next();
    }
}
