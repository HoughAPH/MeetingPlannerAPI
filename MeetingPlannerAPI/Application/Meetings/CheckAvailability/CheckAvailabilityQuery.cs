namespace MeetingPlannerAPI.Application.Meetings.CheckAvailability;

/// <param name="ExcludeMeetingId">A meeting to ignore, so a meeting being edited does not conflict with itself.</param>
public sealed record CheckAvailabilityQuery(
    IReadOnlyCollection<string> Emails,
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? ExcludeMeetingId = null);

public sealed record ParticipantConflictDto(
    string Email,
    Guid MeetingId,
    string Title,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc);

public sealed record ParticipantAvailabilityDto(
    string Email,
    bool IsAvailable,
    IReadOnlyList<ParticipantConflictDto> Conflicts);
