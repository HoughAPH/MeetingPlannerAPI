using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings.GetMeeting;

public sealed class GetMeetingHandler(IMeetingRepository meetings)
{
    /// <summary>Returns the meeting, or null if it does not exist or has been cancelled (cancelled meetings are not part of the calendar).</summary>
    public async Task<MeetingDto?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        Meeting? @meeting = await meetings.GetByIdAsync(id, cancellationToken);
        return @meeting is null || @meeting.IsCancelled ? null : @meeting.ToDto();
    }
}
