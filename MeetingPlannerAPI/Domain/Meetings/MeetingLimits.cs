namespace MeetingPlannerAPI.Domain.Meetings;

/// <summary>Single source of truth for field size limits (domain, API validation and DB mapping).</summary>
public static class MeetingLimits
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int ParticipantNameMaxLength = 100;
    public const int ParticipantEmailMaxLength = 254;
    public const int MaxParticipants = 50;
}
