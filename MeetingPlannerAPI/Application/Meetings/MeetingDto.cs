using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings;

public sealed record ParticipantDto(Guid Id, string Name, string Email, ResponseStatus Response);

public sealed record MeetingDto(
    Guid Id,
    string Title,
    string Description,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    DateTime CreatedAtUtc,
    int Version,
    IReadOnlyList<ParticipantDto> Participants);
