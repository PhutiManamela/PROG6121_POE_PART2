namespace RaceDay.Api.DTOs;

public record CaptureResultRequest(TimeSpan FinishTime, int? Position);

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
    DateTime RecordedAt);
