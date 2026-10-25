using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings.CheckAvailability;

/// <summary>
/// Reports which participants are already booked on another meeting in a time window. Advisory only:
/// nothing is blocked, because overlapping meetings are allowed.
/// </summary>
public sealed class CheckAvailabilityHandler(IMeetingRepository meetings)
{
    public async Task<IReadOnlyList<ParticipantAvailabilityDto>> HandleAsync(
        CheckAvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ToUtc <= query.FromUtc)
            throw new RequestValidationException("'to' must be later than 'from'.");

        List<string> emails = [.. query.Emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()];

        if (emails.Count == 0)
            throw new RequestValidationException("At least one email is required.");
        if (emails.Count > MeetingLimits.MaxParticipants)
            throw new RequestValidationException($"At most {MeetingLimits.MaxParticipants} emails can be checked at once.");

        IReadOnlyList<ParticipantConflict> conflicts = await meetings.FindParticipantConflictsAsync(
            emails, query.FromUtc, query.ToUtc, query.ExcludeMeetingId, cancellationToken);

        ILookup<string, ParticipantConflictDto> byEmail = conflicts.ToLookup(
            c => c.Email,
            c => new ParticipantConflictDto(c.Email, c.MeetingId, c.Title, c.StartTimeUtc, c.EndTimeUtc));

        return [.. emails.Select(email =>
        {
            List<ParticipantConflictDto> own = [.. byEmail[email]];
            return new ParticipantAvailabilityDto(email, own.Count == 0, own);
        })];
    }
}
