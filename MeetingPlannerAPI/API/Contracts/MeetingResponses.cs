using System.Text.Json.Serialization;

namespace MeetingPlannerAPI.API.Contracts;

public sealed record ParticipantResponse(Guid Id, string Name, string Email, bool IsAttending);

public sealed record MeetingResponse(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset CreatedAt,
    int Version,
    IReadOnlyList<ParticipantResponse> Participants,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<AvailabilityConflictResponse>? AvailabilityConflicts = null);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
