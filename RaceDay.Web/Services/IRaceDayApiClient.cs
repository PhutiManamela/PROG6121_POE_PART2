using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>Everything the MVC app can ask the RaceDay API to do. One method per endpoint in docs/endpoints.md.</summary>
public interface IRaceDayApiClient
{
    // Authentication
    Task<ApiResult<AuthResponse>> LoginAsync(string email, string password);
    Task<ApiResult<AuthResponse>> RegisterAsync(RegisterRequest request);

    // Profile
    Task<ApiResult<UserProfile>> GetProfileAsync(int userId);
    Task<ApiResult<UserProfile>> UpdateProfileAsync(int userId, ProfileUpdateRequest request);
    Task<ApiResult<UploadResult>> UploadProfilePictureAsync(int userId, IFormFile file);

    // Events
    Task<ApiResult<List<EventSummary>>> GetEventsAsync(string? search = null, DateTime? date = null);
    Task<ApiResult<EventDetail>> GetEventAsync(int eventId);
    Task<ApiResult<EventDetail>> CreateEventAsync(EventRequest request);
    Task<ApiResult<EventDetail>> UpdateEventAsync(int eventId, EventRequest request);
    Task<ApiResult> DeleteEventAsync(int eventId);
    Task<ApiResult<UploadResult>> UploadBannerAsync(int eventId, IFormFile file);

    // Categories
    Task<ApiResult<List<CategoryItem>>> GetCategoriesAsync(int eventId);
    Task<ApiResult<CategoryItem>> CreateCategoryAsync(int eventId, CategoryRequest request);
    Task<ApiResult<CategoryItem>> UpdateCategoryAsync(int categoryId, CategoryRequest request);
    Task<ApiResult> DeleteCategoryAsync(int categoryId);

    // Enrolments
    Task<ApiResult<EnrolmentItem>> EnrolAsync(int categoryId);
    Task<ApiResult<List<EnrolmentItem>>> GetMyEnrolmentsAsync();
    Task<ApiResult<List<EnrolmentItem>>> GetEventParticipantsAsync(int eventId);
    Task<ApiResult> CancelEnrolmentAsync(int enrolmentId);

    // Results
    Task<ApiResult<ResultItem>> CaptureResultAsync(int enrolmentId, ResultRequest request);
    Task<ApiResult<ResultItem>> UpdateResultAsync(int resultId, ResultRequest request);
    Task<ApiResult<List<ResultItem>>> GetMyResultsAsync();
    Task<ApiResult<List<ResultItem>>> GetLeaderboardAsync(int categoryId);
}
