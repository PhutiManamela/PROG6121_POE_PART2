using System.Net;

namespace RaceDay.Web.Services;

/// <summary>Outcome of a call to the RaceDay API: either success, or a friendly error message to show the user.</summary>
public class ApiResult
{
    public bool Success { get; init; }
    public HttpStatusCode StatusCode { get; init; }
    public string? Error { get; init; }

    /// <summary>True when the API rejected our token (expired or missing): the user must log in again.</summary>
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }
}
