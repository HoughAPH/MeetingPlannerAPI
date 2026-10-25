using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Application.Interfaces;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings.UpdateMeeting;

public sealed class UpdateMeetingHandler(
    IMeetingRepository meetings,
    IUnitOfWork unitOfWork,
    INotificationService notifications)
{
    /// <summary>Updates the meeting. Returns null if it does not exist; throws <see cref="ConcurrencyConflictException"/> if the client's version is stale.</summary>
    public async Task<MeetingDto?> HandleAsync(UpdateMeetingCommand command, CancellationToken cancellationToken)
    {
        Meeting? @meeting = await meetings.GetByIdAsync(command.MeetingId, cancellationToken);
        if (@meeting is null)
            return null;

        // Stale client: it edited a version that has since been replaced.
        if (@meeting.Version != command.ExpectedVersion)
            throw new ConcurrencyConflictException(
                $"The meeting is at version {@meeting.Version}, not {command.ExpectedVersion}. Reload it and try again.");

        @meeting.Update(
            command.Title,
            command.Description,
            command.StartTimeUtc,
            command.EndTimeUtc,
            command.Participants);

        // Simultaneous request: if another write landed after our read, "WHERE Version = @original" matches
        // no row and AppDbContext throws ConcurrencyConflictException.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        MeetingDto dto = @meeting.ToDto();

        await notifications.SendAsync(
            new Notification(
                NotificationType.MeetingUpdated,
                dto.Id,
                dto.Title,
                dto.StartTimeUtc,
                dto.EndTimeUtc,
                [.. dto.Participants.Select(a => a.Email)]),
            cancellationToken);

        return dto;
    }
}
