using MeetingPlannerAPI.Domain.Meetings;
using System.ComponentModel.DataAnnotations;

namespace MeetingPlannerAPI.API.Contracts;

public sealed class CreateMeetingRequest
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

public sealed class ParticipantRequest
{
    [Required, StringLength(MeetingLimits.ParticipantNameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(MeetingLimits.ParticipantEmailMaxLength), EmailAddress]
    public string Email { get; init; } = string.Empty;
}
