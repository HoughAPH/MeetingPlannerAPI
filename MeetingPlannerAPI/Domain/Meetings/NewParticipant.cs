namespace MeetingPlannerAPI.Domain.Meetings;

/// <summary>Input needed to add a participant to a meeting; the <see cref="Participant"/> entity itself can only be created by the aggregate.</summary>
public sealed record NewParticipant(string Name, string Email);
