using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings.RespondToInvitation;

public sealed class RespondToInvitationHandler(IMeetingRepository meetings, IUnitOfWork unitOfWork)
{
    /// <summary>
    /// Records the response and returns the meeting, or null if the meeting or participant does not exist. Repeating the
    /// same response is a no-op. A response is a reply from the participant, so it needs no If-Match; if it collides with
    /// a simultaneous edit the save throws <see cref="ConcurrencyConflictException"/> and the client can simply retry.
    /// </summary>
    public async Task<MeetingDto?> HandleAsync(RespondToInvitationCommand command, CancellationToken cancellationToken)
    {
        Meeting? @meeting = await meetings.GetByIdAsync(command.MeetingId, cancellationToken);
        if (@meeting is null || @meeting.Participants.All(p => p.Id != command.ParticipantId))
            return null;

        if (@meeting.Respond(command.ParticipantId, command.Response))
            await unitOfWork.SaveChangesAsync(cancellationToken);

        return @meeting.ToDto();
    }
}
