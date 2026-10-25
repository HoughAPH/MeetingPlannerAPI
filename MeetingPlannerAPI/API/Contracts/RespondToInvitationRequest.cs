using MeetingPlannerAPI.Domain.Meetings;
using System.ComponentModel.DataAnnotations;

namespace MeetingPlannerAPI.API.Contracts;

public sealed class RespondToInvitationRequest
{
    /// <summary><c>Accepted</c> or <c>Declined</c>.</summary>
    [Required]
    public ResponseStatus? Response { get; init; }
}
