namespace MeetingPlannerAPI.Domain.Meetings;

/// <param name="FromUtc">Only meetings ending after this instant (overlap semantics).</param>
/// <param name="ToUtc">Only meetings starting before this instant (overlap semantics).</param>
/// <param name="Text">Case-insensitive match against title or description.</param>
/// <param name="ParticipantEmail">Only meetings that include this participant.</param>
public sealed record MeetingSearchCriteria(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Text,
    string? ParticipantEmail,
    int Page,
    int PageSize);
