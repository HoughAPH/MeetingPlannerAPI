using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings.CreateMeeting;

/// <summary>Times must already be UTC (<see cref="DateTimeKind.Utc"/>); the API layer normalises offsets.</summary>
public sealed record CreateMeetingCommand(
    string Title,
    string? Description,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    IReadOnlyList<NewParticipant> Participants);
