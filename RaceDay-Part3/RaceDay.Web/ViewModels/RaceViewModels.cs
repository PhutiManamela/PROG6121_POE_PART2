using RaceDay.Web.Models;

namespace RaceDay.Web.ViewModels;

public class RaceCatalogViewModel
{
    public List<EventSummary> Events { get; set; } = new();
    public string? Search { get; set; }
    public DateTime? Date { get; set; }
    public string? EventType { get; set; }
}

public class RaceDetailsViewModel
{
    public EventDetail Event { get; set; } = null!;

    /// <summary>Categories the logged-in participant is already enrolled in (so the button can say "Enrolled").</summary>
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
}

public class LeaderboardViewModel
{
    public int EventId { get; set; }
    public int CategoryId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public List<ResultItem> Results { get; set; } = new();
}
