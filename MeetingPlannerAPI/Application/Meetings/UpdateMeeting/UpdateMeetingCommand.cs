using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings.UpdateMeeting;

/// <summary>Times must already be UTC; <paramref name="ExpectedVersion"/> is the version the client last read (from If-Match).</summary>
public sealed record UpdateMeetingCommand(
    Guid MeetingId,
    int ExpectedVersion,
    string Title,
    string? Description,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    IReadOnlyList<NewParticipant> Participants);
