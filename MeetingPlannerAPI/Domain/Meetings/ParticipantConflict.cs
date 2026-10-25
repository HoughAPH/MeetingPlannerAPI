namespace MeetingPlannerAPI.Domain.Meetings;

/// <summary>A participant (by email) who is already on another, non-cancelled meeting that overlaps a time window.</summary>
public sealed record ParticipantConflict(
    string Email,
    Guid MeetingId,
    string Title,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc);
