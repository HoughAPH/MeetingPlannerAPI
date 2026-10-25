using MeetingPlannerAPI.Application.Meetings;
using MeetingPlannerAPI.Application.Meetings.CheckAvailability;
using MeetingPlannerAPI.Application.Meetings.CreateMeeting;
using MeetingPlannerAPI.Application.Meetings.SearchMeetings;
using MeetingPlannerAPI.Application.Meetings.UpdateMeeting;
using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.API.Contracts;

internal static class ContractMappings
{
    public static CreateMeetingCommand ToCommand(this CreateMeetingRequest request) => new(
        request.Title,
        request.Description,
        request.StartTime!.Value.UtcDateTime,
        request.EndTime!.Value.UtcDateTime,
        request.Participants.Select(a => new NewParticipant(a.Name, a.Email)).ToList());

    public static UpdateMeetingCommand ToCommand(this UpdateMeetingRequest request, Guid id, int expectedVersion) => new(
        id,
        expectedVersion,
        request.Title,
        request.Description,
        request.StartTime!.Value.UtcDateTime,
        request.EndTime!.Value.UtcDateTime,
        request.Participants.Select(a => new NewParticipant(a.Name, a.Email)).ToList());

    public static SearchMeetingsQuery ToQuery(this SearchMeetingsRequest request) => new(
        request.From?.UtcDateTime,
        request.To?.UtcDateTime,
        request.Q,
        request.ParticipantEmail,
        request.Page,
        request.PageSize);

    public static CheckAvailabilityQuery ToQuery(this AvailabilityRequest request, Guid? excludeMeetingId = null) => new(
        request.Emails,
        request.From!.Value.UtcDateTime,
        request.To!.Value.UtcDateTime,
        excludeMeetingId);

    public static AvailabilityConflictResponse ToResponse(this ParticipantConflictDto dto) => new(
        dto.Email,
        dto.MeetingId,
        dto.Title,
        ToUtcOffset(dto.StartTimeUtc),
        ToUtcOffset(dto.EndTimeUtc));

    public static ParticipantAvailabilityResponse ToResponse(this ParticipantAvailabilityDto dto) => new(
        dto.Email,
        dto.IsAvailable,
        dto.Conflicts.Select(c => c.ToResponse()).ToList());

    /// <summary>Attaches advisory availability conflicts; null when there are none so the field is omitted.</summary>
    public static MeetingResponse ToResponse(this MeetingDto dto, IReadOnlyList<ParticipantConflictDto> conflicts)
    {
        MeetingResponse response = dto.ToResponse();
        return conflicts.Count == 0
            ? response
            : response with { AvailabilityConflicts = conflicts.Select(c => c.ToResponse()).ToList() };
    }

    public static MeetingResponse ToResponse(this MeetingDto dto) => new(
        dto.Id,
        dto.Title,
        dto.Description,
        ToUtcOffset(dto.StartTimeUtc),
        ToUtcOffset(dto.EndTimeUtc),
        ToUtcOffset(dto.CreatedAtUtc),
        dto.Version,
        dto.Participants.Select(a => new ParticipantResponse(a.Id, a.Name, a.Email, a.IsAttending)).ToList());

    public static PagedResponse<MeetingResponse> ToResponse(this PagedResult<MeetingDto> page) => new(
        page.Items.Select(ToResponse).ToList(),
        page.Page,
        page.PageSize,
        page.TotalCount);

    private static DateTimeOffset ToUtcOffset(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero);
}
