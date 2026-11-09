namespace RaceDay.Api.Models;

/// <summary>
/// A race event created by an Organiser. Matches the Event entity in the Part 1 ERD.
/// </summary>
public class Event
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;

    /// <summary>Overall event distance in kilometres.</summary>
    public decimal DistanceKm { get; set; }

    /// <summary>"Run", "Walk" or "Cycle".</summary>
    public string EventType { get; set; } = "Run";

    /// <summary>URL of the banner image in Azure Blob Storage (uploaded through the API).</summary>
    public string? BannerUrl { get; set; }

    public int OrganiserId { get; set; }
    public User? Organiser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
