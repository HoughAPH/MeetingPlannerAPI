using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Application.Meetings;
using MeetingPlannerAPI.Application.Meetings.SearchMeetings;
using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPIProject.Tests;

public class SearchMeetingsHandlerTests
{
    private static readonly DateTime From = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Search_NormalisesTextEmailAndPaging_BeforeCallingTheRepository()
    {
        FakeRepository repo = new();

        await new SearchMeetingsHandler(repo).HandleAsync(
            new SearchMeetingsQuery(From, From.AddDays(1), "  standup ", "  Jane@Example.com ", Page: 0, PageSize: 1000), default);

        MeetingSearchCriteria criteria = repo.LastCriteria!;
        Assert.Equal("standup", criteria.Text);
        Assert.Equal("jane@example.com", criteria.ParticipantEmail);
        Assert.Equal(From, criteria.FromUtc);
        Assert.Equal(From.AddDays(1), criteria.ToUtc);
        Assert.Equal(1, criteria.Page);
        Assert.Equal(SearchMeetingsQuery.MaxPageSize, criteria.PageSize);
    }

    [Fact]
    public async Task Search_BlankFilters_BecomeNull_AndPageSizeHasAFloorOfOne()
    {
        FakeRepository repo = new();

        await new SearchMeetingsHandler(repo).HandleAsync(new SearchMeetingsQuery(null, null, "   ", "", PageSize: 0), default);

        Assert.Null(repo.LastCriteria!.Text);
        Assert.Null(repo.LastCriteria.ParticipantEmail);
        Assert.Equal(1, repo.LastCriteria.PageSize);
    }

    [Fact]
    public async Task Search_FromAfterTo_ThrowsValidationAndSkipsRepository()
    {
        FakeRepository repo = new();

        await Assert.ThrowsAsync<RequestValidationException>(() => new SearchMeetingsHandler(repo).HandleAsync(
            new SearchMeetingsQuery(From.AddHours(1), From, null, null), default));

        Assert.Null(repo.LastCriteria);
    }

    [Fact]
    public async Task Search_MapsMeetingsToDtos_AndKeepsPagingTotals()
    {
        Meeting meeting = Meeting.Create("Standup", null, From, From.AddHours(1), [new NewParticipant("Jane", "jane@example.com")]);
        FakeRepository repo = new() { Result = new PagedResult<Meeting>([meeting], 2, 10, 11) };

        PagedResult<MeetingDto> result = await new SearchMeetingsHandler(repo).HandleAsync(
            new SearchMeetingsQuery(null, null, null, null, Page: 2, PageSize: 10), default);

        MeetingDto dto = Assert.Single(result.Items);
        Assert.Equal(meeting.Id, dto.Id);
        Assert.Equal("jane@example.com", Assert.Single(dto.Participants).Email);
        Assert.Equal((2, 10, 11), (result.Page, result.PageSize, result.TotalCount));
    }

    private sealed class FakeRepository : IMeetingRepository
    {
        public PagedResult<Meeting> Result { get; init; } = new([], 1, SearchMeetingsQuery.DefaultPageSize, 0);
        public MeetingSearchCriteria? LastCriteria { get; private set; }

        public Task<PagedResult<Meeting>> SearchAsync(MeetingSearchCriteria criteria, CancellationToken cancellationToken)
        {
            LastCriteria = criteria;
            return Task.FromResult(Result);
        }

        public Task AddAsync(Meeting meeting, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Meeting?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> IsCancelledAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ParticipantConflict>> FindParticipantConflictsAsync(
            IReadOnlyCollection<string> emails, DateTime fromUtc, DateTime toUtc, Guid? excludeMeetingId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
