using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings.RespondToInvitation;

public sealed record RespondToInvitationCommand(Guid MeetingId, Guid ParticipantId, ResponseStatus Response);
