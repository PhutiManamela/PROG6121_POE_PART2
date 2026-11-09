namespace RaceDay.Api.Services;

/// <summary>
/// Reads the current caller's identity regardless of how they authenticated:
/// a JWT bearer token (resolved by <c>RequireSessionAttribute</c> into HttpContext.Items) or the session cookie.
/// </summary>
public static class HttpContextExtensions
{
    public static int? GetUserId(this HttpContext context)
    {
        if (context.Items.TryGetValue(SessionKeys.UserId, out var value) && value is int id)
        {
            return id;
        }

        return context.Session.GetInt32(SessionKeys.UserId);
    }

    public static string? GetUserRole(this HttpContext context)
    {
        if (context.Items.TryGetValue(SessionKeys.Role, out var value) && value is string role)
        {
            return role;
        }

        return context.Session.GetString(SessionKeys.Role);
    }
}
