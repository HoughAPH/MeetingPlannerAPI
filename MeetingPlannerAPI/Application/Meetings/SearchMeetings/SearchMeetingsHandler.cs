using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPI.Application.Meetings.SearchMeetings;

public sealed class SearchMeetingsHandler(IMeetingRepository meetings)
{
    public async Task<PagedResult<MeetingDto>> HandleAsync(SearchMeetingsQuery query, CancellationToken cancellationToken)
    {
        if (query.FromUtc is not null && query.ToUtc is not null)
        {
            DateTime from = query.FromUtc.Value;
            DateTime to = query.ToUtc.Value;

            if (from > to)
                throw new RequestValidationException("'from' must not be later than 'to'.");
        }

        int page = Math.Max(query.Page, 1);
        int pageSize = Math.Clamp(query.PageSize, 1, SearchMeetingsQuery.MaxPageSize);

        MeetingSearchCriteria criteria = new(
            query.FromUtc,
            query.ToUtc,
            string.IsNullOrWhiteSpace(query.Text) ? null : query.Text.Trim(),
            string.IsNullOrWhiteSpace(query.ParticipantEmail) ? null : query.ParticipantEmail.Trim().ToLowerInvariant(),
            page,
            pageSize);

        PagedResult<Meeting> result = await meetings.SearchAsync(criteria, cancellationToken);

        return new PagedResult<MeetingDto>(
            result.Items.Select(e => e.ToDto()).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount);
    }
}
