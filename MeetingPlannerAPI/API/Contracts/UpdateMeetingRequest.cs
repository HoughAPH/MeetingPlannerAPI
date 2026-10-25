using MeetingPlannerAPI.Domain.Meetings;
using System.ComponentModel.DataAnnotations;

namespace MeetingPlannerAPI.API.Contracts;

/// <summary>Full replacement of a meeting's details and participant list. Participants omitted here are removed.</summary>
public sealed class UpdateMeetingRequest
{
    [Required, StringLength(MeetingLimits.TitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    [StringLength(MeetingLimits.DescriptionMaxLength)]
    public string? Description { get; init; }

    [Required]
    public DateTimeOffset? StartTime { get; init; }

    [Required]
    public DateTimeOffset? EndTime { get; init; }

    [MaxLength(MeetingLimits.MaxParticipants)]
    public List<ParticipantRequest> Participants { get; init; } = [];
}
