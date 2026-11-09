using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Helpers;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>
/// Typed HttpClient for the RaceDay API (registered through IHttpClientFactory in Program.cs).
/// Every call attaches the logged-in user's JWT from session as "Authorization: Bearer {token}".
/// Files are forwarded to the API as multipart/form-data: the API, not this app, talks to Azure Blob Storage.
/// </summary>
public class RaceDayApiClient : IRaceDayApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;

    public RaceDayApiClient(HttpClient http, IHttpContextAccessor context)
    {
        _http = http;
        _context = context;
    }

    // ---- Authentication
    public Task<ApiResult<AuthResponse>> LoginAsync(string email, string password) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", JsonBody(new LoginRequest(email, password)));

    public Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", JsonBody(request));

    // ---- Profile
    public Task<ApiResult<UserProfile>> GetProfileAsync(int userId) =>
        SendAsync<UserProfile>(HttpMethod.Get, $"api/users/{userId}");

    public Task<ApiResult<UserProfile>> UpdateProfileAsync(int userId, ProfileUpdateRequest request) =>
        SendAsync<UserProfile>(HttpMethod.Put, $"api/users/{userId}", JsonBody(request));

    public Task<ApiResult<UploadResult>> UploadProfilePictureAsync(int userId, IFormFile file) =>
        SendAsync<UploadResult>(HttpMethod.Post, $"api/users/{userId}/profile-picture", FileBody(file));

    // ---- Events
    public Task<ApiResult<List<EventSummary>>> GetEventsAsync(string? search = null, DateTime? date = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (date.HasValue)
        {
            query.Add($"date={date.Value:yyyy-MM-dd}");
        }

        var url = query.Count == 0 ? "api/events" : $"api/events?{string.Join('&', query)}";
        return SendAsync<List<EventSummary>>(HttpMethod.Get, url);
    }

    public Task<ApiResult<EventDetail>> GetEventAsync(int eventId) =>
        SendAsync<EventDetail>(HttpMethod.Get, $"api/events/{eventId}");

    public Task<ApiResult<EventDetail>> CreateEventAsync(EventRequest request) =>
        SendAsync<EventDetail>(HttpMethod.Post, "api/events", JsonBody(request));

    public Task<ApiResult<EventDetail>> UpdateEventAsync(int eventId, EventRequest request) =>
        SendAsync<EventDetail>(HttpMethod.Put, $"api/events/{eventId}", JsonBody(request));

    public async Task<ApiResult> DeleteEventAsync(int eventId) =>
        await SendAsync<object>(HttpMethod.Delete, $"api/events/{eventId}");

    public Task<ApiResult<UploadResult>> UploadBannerAsync(int eventId, IFormFile file) =>
        SendAsync<UploadResult>(HttpMethod.Post, $"api/events/{eventId}/banner", FileBody(file));

    // ---- Categories
    public Task<ApiResult<List<CategoryItem>>> GetCategoriesAsync(int eventId) =>
        SendAsync<List<CategoryItem>>(HttpMethod.Get, $"api/events/{eventId}/categories");

    public Task<ApiResult<CategoryItem>> CreateCategoryAsync(int eventId, CategoryRequest request) =>
        SendAsync<CategoryItem>(HttpMethod.Post, $"api/events/{eventId}/categories", JsonBody(request));

    public Task<ApiResult<CategoryItem>> UpdateCategoryAsync(int categoryId, CategoryRequest request) =>
        SendAsync<CategoryItem>(HttpMethod.Put, $"api/categories/{categoryId}", JsonBody(request));

    public async Task<ApiResult> DeleteCategoryAsync(int categoryId) =>
        await SendAsync<object>(HttpMethod.Delete, $"api/categories/{categoryId}");

    // ---- Enrolments
    public Task<ApiResult<EnrolmentItem>> EnrolAsync(int categoryId) =>
        SendAsync<EnrolmentItem>(HttpMethod.Post, $"api/categories/{categoryId}/enrolments");

    public Task<ApiResult<List<EnrolmentItem>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentItem>>(HttpMethod.Get, "api/enrolments/me");

    public Task<ApiResult<List<EnrolmentItem>>> GetEventParticipantsAsync(int eventId) =>
        SendAsync<List<EnrolmentItem>>(HttpMethod.Get, $"api/events/{eventId}/participants");

    public async Task<ApiResult> CancelEnrolmentAsync(int enrolmentId) =>
        await SendAsync<object>(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");

    // ---- Results
    public Task<ApiResult<ResultItem>> CaptureResultAsync(int enrolmentId, ResultRequest request) =>
        SendAsync<ResultItem>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/result", JsonBody(request));

    public Task<ApiResult<ResultItem>> UpdateResultAsync(int resultId, ResultRequest request) =>
        SendAsync<ResultItem>(HttpMethod.Put, $"api/results/{resultId}", JsonBody(request));

    public Task<ApiResult<List<ResultItem>>> GetMyResultsAsync() =>
        SendAsync<List<ResultItem>>(HttpMethod.Get, "api/results/me");

    public Task<ApiResult<List<ResultItem>>> GetLeaderboardAsync(int categoryId) =>
        SendAsync<List<ResultItem>>(HttpMethod.Get, $"api/categories/{categoryId}/leaderboard");

    // ---- Plumbing
    private static HttpContent JsonBody<T>(T value) => JsonContent.Create(value, options: Json);

    private static HttpContent FileBody(IFormFile file)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        form.Add(fileContent, "file", file.FileName);
        return form;
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, HttpContent? content = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url) { Content = content };

            var token = _context.HttpContext?.Session.GetString(SessionKeys.Token);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult<T>
                {
                    Success = false,
                    StatusCode = response.StatusCode,
                    Error = await ReadErrorAsync(response)
                };
            }

            T? data = default;
            var body = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(body))
            {
                data = JsonSerializer.Deserialize<T>(body, Json);
            }

            return new ApiResult<T> { Success = true, StatusCode = response.StatusCode, Data = data };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new ApiResult<T>
            {
                Success = false,
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Error = "We couldn't reach the RaceDay service. Please try again in a moment."
            };
        }
    }

    /// <summary>Turns an API error body ({ "message": "..." } or ASP.NET validation problem details) into text for the user.</summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString()!;
                }

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        foreach (var item in field.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                return item.GetString()!;
                            }
                        }
                    }
                }

                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                {
                    return title.GetString()!;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // fall through to a generic message
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Please log in to continue.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.NotFound => "We couldn't find what you were looking for.",
            HttpStatusCode.Conflict => "That conflicts with existing data.",
            _ => "Something went wrong. Please try again."
        };
    }
}
