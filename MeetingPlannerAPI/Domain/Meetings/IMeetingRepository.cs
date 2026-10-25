using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Domain.Meetings;

public interface IMeetingRepository
{
    Task AddAsync(Meeting @meeting, CancellationToken cancellationToken);

    /// <summary>Returns the tracked meeting with its participants, or null if it does not exist.</summary>
    Task<Meeting?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Reads the cancellation state from the store, bypassing any tracked (possibly stale) instance.</summary>
    Task<bool> IsCancelledAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<Meeting>> SearchAsync(MeetingSearchCriteria criteria, CancellationToken cancellationToken);

    /// <summary>
    /// Finds non-cancelled meetings overlapping [fromUtc, toUtc) that include any of the (lower-cased) emails,
    /// optionally ignoring one meeting (the one being edited). Touching boundaries do not overlap.
    /// </summary>
    Task<IReadOnlyList<ParticipantConflict>> FindParticipantConflictsAsync(
        IReadOnlyCollection<string> emails,
        DateTime fromUtc,
        DateTime toUtc,
        Guid? excludeMeetingId,
        CancellationToken cancellationToken);
}
