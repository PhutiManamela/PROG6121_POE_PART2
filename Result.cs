namespace RaceDay.Api.Models;

/// <summary>
/// The finish time and position recorded for one enrolment. Matches the Result entity in the Part 1 ERD.
/// </summary>
public class Result
{
    public int ResultId { get; set; }

    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }

    public TimeSpan FinishTime { get; set; }
    public int? Position { get; set; }

    /// <summary>The organiser who captured this result.</summary>
    public int RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
