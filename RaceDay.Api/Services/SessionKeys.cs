namespace RaceDay.Api.Services;

/// <summary>Keys used to store a logged-in user's identity and role in ASP.NET Core session state.</summary>
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string FullName = "FullName";
}
