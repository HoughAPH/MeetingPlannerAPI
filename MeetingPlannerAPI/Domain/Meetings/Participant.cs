using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace MeetingPlannerAPI.Domain.Meetings;

public sealed class Participant : Entity
{
    public string Name { get; private set; } = string.Empty;

    /// <summary>Stored trimmed and lower-cased so equality and lookups are case-insensitive.</summary>
    [EmailAddress]
    public string Email { get; private set; } = string.Empty;

    public bool IsAttending { get; private set; }

    private Participant()
    {
    }

    internal static Participant Create(string name, string email)
    {
        name = (name ?? string.Empty).Trim();
        email = (email ?? string.Empty).Trim();

        if (name.Length == 0)
            throw new DomainException("participant.name.required", "Participant name is required.");
        if (name.Length > MeetingLimits.ParticipantNameMaxLength)
            throw new DomainException("participant.name.too_long",
                $"Participant name must be at most {MeetingLimits.ParticipantNameMaxLength} characters.");

        if (email.Length == 0)
            throw new DomainException("participant.email.required", "Participant email is required.");
        if (email.Length > MeetingLimits.ParticipantEmailMaxLength)
            throw new DomainException("participant.email.too_long",
                $"Participant email must be at most {MeetingLimits.ParticipantEmailMaxLength} characters.");
        if (!IsValidEmail(email))
            throw new DomainException("participant.email.invalid", $"'{email}' is not a valid email address.");

        return new Participant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email.ToLowerInvariant(),
            IsAttending = false
        };
    }

    /// <summary>Called by <see cref="Meeting"/> with a name already validated by <see cref="Create"/>.</summary>
    internal void Rename(string name) => Name = name;

    // Deliberately simple structural check; full RFC 5322 validation is not worth its complexity here.
    private static bool IsValidEmail(string email) =>
        MailAddress.TryCreate(email, out MailAddress? parsed)
        && parsed.Address == email
        && parsed.Host.Contains('.');
}
