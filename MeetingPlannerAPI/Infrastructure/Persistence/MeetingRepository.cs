using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;
using Microsoft.EntityFrameworkCore;

namespace MeetingPlannerAPI.Infrastructure.Persistence;

internal sealed class MeetingRepository(AppDbContext db) : IMeetingRepository
{
    private const string LikeEscape = "\\";

    public async Task AddAsync(Meeting @meeting, CancellationToken cancellationToken) =>
        await db.Meetings.AddAsync(@meeting, cancellationToken);

    public Task<Meeting?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Meetings.Include(e => e.Participants).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<bool> IsCancelledAsync(Guid id, CancellationToken cancellationToken) =>
        db.Meetings.AsNoTracking().AnyAsync(e => e.Id == id && e.CancelledAtUtc != null, cancellationToken);

    public async Task<PagedResult<Meeting>> SearchAsync(MeetingSearchCriteria criteria, CancellationToken cancellationToken)
    {
        // Cancelled meetings are not part of the calendar.
        IQueryable<Meeting> query = db.Meetings.AsNoTracking().Where(e => e.CancelledAtUtc == null);

        // Overlap semantics: a meeting matches if it ends after 'from' and starts before 'to'.
        if (criteria.FromUtc is not null)
        {
            DateTime from = criteria.FromUtc.Value;
            query = query.Where(e => e.EndTimeUtc > from);
        }

        if (criteria.ToUtc is not null)
        {
            DateTime to = criteria.ToUtc.Value;
            query = query.Where(e => e.StartTimeUtc < to);
        }

        if (criteria.Text is not null)
        {
            string text = criteria.Text;
            string pattern = $"%{EscapeLike(text)}%";
            query = query.Where(e =>
                EF.Functions.Like(e.Title, pattern, LikeEscape) ||
                EF.Functions.Like(e.Description, pattern, LikeEscape));
        }

        if (criteria.ParticipantEmail is not null)
        {
            string email = criteria.ParticipantEmail;
            query = query.Where(e => e.Participants.Any(a => a.Email == email));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<Meeting> items = await query
            .Include(e => e.Participants)
            .OrderBy(e => e.StartTimeUtc)
            .ThenBy(e => e.Id)
            .Skip((int)Math.Min((long)(criteria.Page - 1) * criteria.PageSize, int.MaxValue))
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Meeting>(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<ParticipantConflict>> FindParticipantConflictsAsync(
        IReadOnlyCollection<string> emails,
        DateTime fromUtc,
        DateTime toUtc,
        Guid? excludeMeetingId,
        CancellationToken cancellationToken)
    {
        // Same overlap semantics as SearchAsync; cancelled meetings do not block anyone, and neither do meetings the participant declined.
        IQueryable<Meeting> query = db.Meetings.AsNoTracking()
            .Where(e => e.CancelledAtUtc == null && e.EndTimeUtc > fromUtc && e.StartTimeUtc < toUtc);

        if (excludeMeetingId is not null)
        {
            Guid excluded = excludeMeetingId.Value;
            query = query.Where(e => e.Id != excluded);
        }

        List<ParticipantConflict> conflicts = await query
            .SelectMany(
                e => e.Participants.Where(a => emails.Contains(a.Email) && a.Response != ResponseStatus.Declined),
                (e, a) => new ParticipantConflict(a.Email, e.Id, e.Title, e.StartTimeUtc, e.EndTimeUtc))
            .ToListAsync(cancellationToken);

        // Ordered in memory: EF cannot translate OrderBy over the projected record's members.
        return [.. conflicts.OrderBy(c => c.StartTimeUtc).ThenBy(c => c.MeetingId)];
    }

    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");
}
