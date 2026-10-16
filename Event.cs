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

    public int OrganiserId { get; set; }
    public User? Organiser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Category> Categories { get; set; } = new List<Category>();
}
