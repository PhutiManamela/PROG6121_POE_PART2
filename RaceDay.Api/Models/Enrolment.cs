namespace RaceDay.Api.Models;

/// <summary>
/// Links a Participant to a Category they have entered. Matches the Enrolment entity in the Part 1 ERD.
/// </summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }
    public User? Participant { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;

    /// <summary>"Confirmed" or "Cancelled".</summary>
    public string Status { get; set; } = "Confirmed";

    public Result? Result { get; set; }
}
