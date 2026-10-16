namespace RaceDay.Api.DTOs;

public record EnrolmentResponse(
    int EnrolmentId,
    int ParticipantId,
    string ParticipantName,
    int CategoryId,
    string CategoryName,
    int EventId,
    string EventName,
    DateTime EnrolmentDate,
    string Status);
