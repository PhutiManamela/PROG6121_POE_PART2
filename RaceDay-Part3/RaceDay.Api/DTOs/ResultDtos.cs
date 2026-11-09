namespace RaceDay.Api.DTOs;

public record CaptureResultRequest(TimeSpan FinishTime, int? Position);

/// <summary>TotalFinishers is how many results exist in the same category (for "47th out of 312 finishers").</summary>
public record ResultResponse(
    int ResultId,
    int EnrolmentId,
    int ParticipantId,
    string ParticipantName,
    int CategoryId,
    string CategoryName,
    int EventId,
    string EventName,
    TimeSpan FinishTime,
    int? Position,
    int TotalFinishers,
    DateTime RecordedAt);
