using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Application.Interfaces;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings.CancelMeeting;

public sealed class CancelMeetingHandler(
    IMeetingRepository meetings,
    IUnitOfWork unitOfWork,
    INotificationService notifications)
{
    /// <summary>Cancels the meeting. Returns false if it does not exist. Cancelling twice is a no-op and sends nothing.</summary>
    public async Task<bool> HandleAsync(CancelMeetingCommand command, CancellationToken cancellationToken)
    {
        Meeting? @meeting = await meetings.GetByIdAsync(command.MeetingId, cancellationToken);
        if (@meeting is null)
            return false;

        if (!@meeting.Cancel())
            return true;

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // A concurrent DELETE won the race and has already notified the participants.
            if (await meetings.IsCancelledAsync(command.MeetingId, cancellationToken))
                return true;

            throw;
        }

        await notifications.SendAsync(
            new Notification(
                NotificationType.MeetingCancelled,
                @meeting.Id,
                @meeting.Title,
                @meeting.StartTimeUtc,
                @meeting.EndTimeUtc,
                @meeting.Participants.Select(a => a.Email).ToList()),
            cancellationToken);

        return true;
    }
}
