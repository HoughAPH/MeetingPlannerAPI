using MeetingPlannerAPI.Domain.Meetings;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace MeetingPlannerAPI.API.Contracts;

public sealed class AvailabilityRequest
{
    /// <summary>Participant emails to check. Repeat the parameter: ?email=a@x.com&amp;email=b@x.com.</summary>
    [FromQuery(Name = "email")]
    [MinLength(1)]
    [MaxLength(MeetingLimits.MaxParticipants)]
    public List<string> Emails { get; init; } = [];

    /// <summary>Start of the window to check.</summary>
    [Required]
    public DateTimeOffset? From { get; init; }

    /// <summary>End of the window to check. Must be later than <c>from</c>.</summary>
    [Required]
    public DateTimeOffset? To { get; init; }
}

public sealed record AvailabilityConflictResponse(
    string Email,
    Guid MeetingId,
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime);

public sealed record ParticipantAvailabilityResponse(
    string Email,
    bool IsAvailable,
    IReadOnlyList<AvailabilityConflictResponse> Conflicts);
