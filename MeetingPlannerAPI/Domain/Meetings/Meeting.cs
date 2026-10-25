using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Domain.Meetings;

public sealed class Meeting : AggregateRoot
{
    private readonly List<Participant> _participants = [];

    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime StartTimeUtc { get; private set; }
    public DateTime EndTimeUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public bool IsCancelled => CancelledAtUtc is not null;

    /// <summary>Optimistic concurrency token. Incremented on every change to the aggregate, including participant-only changes.</summary>
    public int Version { get; private set; }

    public IReadOnlyCollection<Participant> Participants => _participants.AsReadOnly();

    private Meeting()
    {
    }

    public static Meeting Create(
        string title,
        string? description,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        IEnumerable<NewParticipant>? participants)
    {
        (title, description) = ValidateDetails(title, description);
        ValidateTimes(startTimeUtc, endTimeUtc);

        Meeting @meeting = new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            CreatedAtUtc = DateTime.UtcNow,
            Version = 1
        };

        @meeting.SyncParticipants(participants ?? []);

        return @meeting;
    }

    /// <summary>Replaces the meeting's details and participant list. All input is validated before anything changes.</summary>
    public void Update(
        string title,
        string? description,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        IEnumerable<NewParticipant>? participants)
    {
        if (IsCancelled)
            throw new DomainException("meeting.cancelled", "A cancelled meeting cannot be changed.");

        (title, description) = ValidateDetails(title, description);
        ValidateTimes(startTimeUtc, endTimeUtc);
        SyncParticipants(participants ?? []);

        Title = title;
        Description = description;
        StartTimeUtc = startTimeUtc;
        EndTimeUtc = endTimeUtc;
        Version++;
    }

    /// <summary>Marks the meeting as cancelled. Returns false if it was already cancelled (no state change).</summary>
    public bool Cancel()
    {
        if (IsCancelled)
            return false;

        CancelledAtUtc = DateTime.UtcNow;
        Version++;
        return true;
    }

    private static (string Title, string Description) ValidateDetails(string? title, string? description)
    {
        title = (title ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();

        if (title.Length == 0)
            throw new DomainException("meeting.title.required", "Meeting title is required.");
        if (title.Length > MeetingLimits.TitleMaxLength)
            throw new DomainException("meeting.title.too_long",
                $"Meeting title must be at most {MeetingLimits.TitleMaxLength} characters.");
        if (description.Length > MeetingLimits.DescriptionMaxLength)
            throw new DomainException("meeting.description.too_long",
                $"Meeting description must be at most {MeetingLimits.DescriptionMaxLength} characters.");

        return (title, description);
    }

    private static void ValidateTimes(DateTime startTimeUtc, DateTime endTimeUtc)
    {
        if (startTimeUtc.Kind != DateTimeKind.Utc || endTimeUtc.Kind != DateTimeKind.Utc)
            throw new DomainException("meeting.time.not_utc", "Meeting times must be expressed in UTC.");
        if (endTimeUtc <= startTimeUtc)
            throw new DomainException("meeting.time.invalid_range", "Meeting end time must be after its start time.");
    }

    // Validates the full list first, then reconciles by email so existing participants keep their Id and IsAttending.
    private void SyncParticipants(IEnumerable<NewParticipant> participants)
    {
        List<Participant> desired = participants.Select(a => Participant.Create(a.Name, a.Email)).ToList();

        if (desired.Count > MeetingLimits.MaxParticipants)
            throw new DomainException("meeting.participants.too_many",
                $"A meeting can have at most {MeetingLimits.MaxParticipants} participants.");

        string? duplicate = desired.GroupBy(a => a.Email).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicate is not null)
            throw new DomainException("meeting.participants.duplicate",
                $"'{duplicate}' is already a participant of this meeting.");

        _participants.RemoveAll(existing => desired.All(d => d.Email != existing.Email));

        foreach (Participant participant in desired)
        {
            Participant? existing = _participants.Find(a => a.Email == participant.Email);
            if (existing is null)
                _participants.Add(participant);
            else
                existing.Rename(participant.Name);
        }
    }
}
