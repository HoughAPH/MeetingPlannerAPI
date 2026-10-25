using MeetingPlannerAPI.Application.Meetings.SearchMeetings;
using MeetingPlannerAPI.Domain.Meetings;
using System.ComponentModel.DataAnnotations;

namespace MeetingPlannerAPI.API.Contracts;

public sealed class SearchMeetingsRequest
{
    /// <summary>Only meetings ending after this instant.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Only meetings starting before this instant.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Case-insensitive text matched against title and description.</summary>
    [StringLength(MeetingLimits.TitleMaxLength)]
    public string? Q { get; init; }

    /// <summary>Only meetings that include this participant.</summary>
    [StringLength(MeetingLimits.ParticipantEmailMaxLength)]
    public string? ParticipantEmail { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, SearchMeetingsQuery.MaxPageSize)]
    public int PageSize { get; init; } = SearchMeetingsQuery.DefaultPageSize;
}
