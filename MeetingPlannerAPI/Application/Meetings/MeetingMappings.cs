using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings;

internal static class MeetingMappings
{
    public static MeetingDto ToDto(this Meeting @meeting) => new(
        @meeting.Id,
        @meeting.Title,
        @meeting.Description,
        @meeting.StartTimeUtc,
        @meeting.EndTimeUtc,
        @meeting.CreatedAtUtc,
        @meeting.Version,
        @meeting.Participants.Select(a => new ParticipantDto(a.Id, a.Name, a.Email, a.Response)).ToList());
}
