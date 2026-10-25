namespace MeetingPlannerAPI.Application.Meetings.SearchMeetings;

public sealed record SearchMeetingsQuery(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Text,
    string? ParticipantEmail,
    int Page = 1,
    int PageSize = SearchMeetingsQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
