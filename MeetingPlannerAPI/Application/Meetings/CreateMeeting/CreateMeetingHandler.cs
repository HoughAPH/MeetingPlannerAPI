using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Application.Interfaces;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings.CreateMeeting;

public sealed class CreateMeetingHandler(
    IMeetingRepository meetings,
    IUnitOfWork unitOfWork,
    INotificationService notifications)
{
    public async Task<MeetingDto> HandleAsync(CreateMeetingCommand command, CancellationToken cancellationToken)
    {
        Meeting @meeting = Meeting.Create(
            command.Title,
            command.Description,
            command.StartTimeUtc,
            command.EndTimeUtc,
            command.Participants);

        await meetings.AddAsync(@meeting, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        MeetingDto dto = @meeting.ToDto();

        await notifications.SendAsync(
            new Notification(
                NotificationType.MeetingCreated,
                dto.Id,
                dto.Title,
                dto.StartTimeUtc,
                dto.EndTimeUtc,
                [.. dto.Participants.Select(a => a.Email)]),
            cancellationToken);

        return dto;
    }
}
