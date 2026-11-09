using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models;

namespace RaceDay.Web.ViewModels;

public class EventRow
{
    public EventSummary Event { get; set; } = null!;
    public int Participants { get; set; }
}

public class DashboardViewModel
{
    public int HostedEvents { get; set; }
    public int UpcomingEvents { get; set; }
    public int TotalParticipants { get; set; }
    public EventSummary? NextEvent { get; set; }
    public List<EventRow> Rows { get; set; } = new();
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Please give the event a name.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "The description can be at most 500 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose the event date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location.")]
    [StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the distance.")]
    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Please choose an event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    public string? BannerUrl { get; set; }

    [Display(Name = "Event banner")]
    public IFormFile? Banner { get; set; }
}

public class CategoryFormViewModel
{
    public int? CategoryId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please name the category (e.g. 10km, Under 20).")]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the distance.")]
    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Please set a registration limit.")]
    [Range(1, 100000, ErrorMessage = "The limit must be at least 1.")]
    [Display(Name = "Registration limit")]
    public int? MaxParticipants { get; set; }

    [Required(ErrorMessage = "Please enter the entry fee (0 for free).")]
    [Range(0, 100000, ErrorMessage = "The entry fee can't be negative.")]
    [Display(Name = "Entry fee (R)")]
    public decimal? EntryFee { get; set; }
}

public class ParticipantListViewModel
{
    public EventDetail Event { get; set; } = null!;
    public List<EnrolmentItem> Enrolments { get; set; } = new();
    public int? CategoryId { get; set; }
}

public class ResultRow
{
    public EnrolmentItem Enrolment { get; set; } = null!;
    public ResultItem? Result { get; set; }
}

public class CategoryResults
{
    public CategoryItem Category { get; set; } = null!;
    public List<ResultRow> Rows { get; set; } = new();
}

public class ResultsCaptureViewModel
{
    public EventDetail Event { get; set; } = null!;
    public List<CategoryResults> Categories { get; set; } = new();
}
