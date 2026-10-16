namespace RaceDay.Api.Models;

/// <summary>
/// A distance/age category within an event (e.g. 10km, 21km, Marathon). Matches the Category entity in the Part 1 ERD.
/// </summary>
public class Category
{
    public int CategoryId { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int MaxParticipants { get; set; } = 100;
    public decimal EntryFee { get; set; }

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}
