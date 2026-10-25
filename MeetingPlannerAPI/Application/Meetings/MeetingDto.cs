namespace MeetingPlannerAPI.Application.Meetings;

public sealed record ParticipantDto(Guid Id, string Name, string Email, bool IsAttending);

public sealed record MeetingDto(
    Guid Id,
    string Title,
    string Description,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    DateTime CreatedAtUtc,
    int Version,
    IReadOnlyList<ParticipantDto> Participants);
