using System.Text.Json.Serialization;

namespace MeetingPlannerAPI.Domain.Meetings;

/// <summary>A participant's answer to a meeting invitation. Everyone starts with <see cref="NoResponse"/>.</summary>
// Serialised by name so the API contract reads "Accepted" rather than 1.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResponseStatus
{
    NoResponse = 0,
    Accepted = 1,
    Declined = 2
}
