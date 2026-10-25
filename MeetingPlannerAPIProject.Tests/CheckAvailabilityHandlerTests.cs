using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Application.Meetings.CheckAvailability;
using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPIProject.Tests;

public class CheckAvailabilityHandlerTests
{
    private static readonly DateTime From = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddHours(1);

    [Fact]
    public async Task NoConflicts_ReportsEveryoneAvailable()
    {
        FakeRepository repo = new();

        IReadOnlyList<ParticipantAvailabilityDto> result =
            await Handler(repo).HandleAsync(new CheckAvailabilityQuery(["a@example.com", "b@example.com"], From, To), default);

        Assert.Equal(["a@example.com", "b@example.com"], result.Select(r => r.Email));
        Assert.All(result, r =>
        {
            Assert.True(r.IsAvailable);
            Assert.Empty(r.Conflicts);
        });
    }

    [Fact]
    public async Task Conflicts_AreGroupedUnderTheRightParticipant()
    {
        Guid meetingId = Guid.NewGuid();
        FakeRepository repo = new()
        {
            Conflicts = [new ParticipantConflict("jane@example.com", meetingId, "Standup", From, To)]
        };

        IReadOnlyList<ParticipantAvailabilityDto> result =
            await Handler(repo).HandleAsync(new CheckAvailabilityQuery(["jane@example.com", "bob@example.com"], From, To), default);

        ParticipantAvailabilityDto jane = result.Single(r => r.Email == "jane@example.com");
        Assert.False(jane.IsAvailable);
        ParticipantConflictDto conflict = Assert.Single(jane.Conflicts);
        Assert.Equal(meetingId, conflict.MeetingId);
        Assert.Equal("Standup", conflict.Title);
        Assert.True(result.Single(r => r.Email == "bob@example.com").IsAvailable);
    }

    [Fact]
    public async Task Emails_AreTrimmedLowerCasedDeduplicatedAndBlanksDropped()
    {
        FakeRepository repo = new();

        IReadOnlyList<ParticipantAvailabilityDto> result = await Handler(repo).HandleAsync(
            new CheckAvailabilityQuery(["  Jane@Example.com ", "jane@example.com", "", "   "], From, To), default);

        Assert.Equal("jane@example.com", Assert.Single(result).Email);
        Assert.Equal(["jane@example.com"], repo.LastEmails);
    }

    [Fact]
    public async Task WindowAndExcludedMeeting_ArePassedToTheRepository()
    {
        FakeRepository repo = new();
        Guid exclude = Guid.NewGuid();

        await Handler(repo).HandleAsync(new CheckAvailabilityQuery(["a@example.com"], From, To, exclude), default);

        Assert.Equal(From, repo.LastFrom);
        Assert.Equal(To, repo.LastTo);
        Assert.Equal(exclude, repo.LastExclude);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public async Task WindowNotAfterStart_ThrowsValidationAndSkipsRepository(int minutesAfterFrom)
    {
        FakeRepository repo = new();

        await Assert.ThrowsAsync<RequestValidationException>(() => Handler(repo).HandleAsync(
            new CheckAvailabilityQuery(["a@example.com"], From, From.AddMinutes(minutesAfterFrom)), default));

        Assert.Equal(0, repo.Calls);
    }

    [Fact]
    public async Task NoUsableEmails_ThrowsValidation()
    {
        FakeRepository repo = new();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            Handler(repo).HandleAsync(new CheckAvailabilityQuery(["", "  "], From, To), default));
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            Handler(repo).HandleAsync(new CheckAvailabilityQuery([], From, To), default));

        Assert.Equal(0, repo.Calls);
    }

    [Fact]
    public async Task AcceptsExactlyMaxEmails_ButRejectsOneMore()
    {
        static string[] Emails(int count) => [.. Enumerable.Range(0, count).Select(i => $"p{i}@example.com")];
        FakeRepository repo = new();

        Assert.Equal(MeetingLimits.MaxParticipants,
            (await Handler(repo).HandleAsync(new CheckAvailabilityQuery(Emails(MeetingLimits.MaxParticipants), From, To), default)).Count);

        await Assert.ThrowsAsync<RequestValidationException>(() => Handler(repo).HandleAsync(
            new CheckAvailabilityQuery(Emails(MeetingLimits.MaxParticipants + 1), From, To), default));
    }

    private static CheckAvailabilityHandler Handler(IMeetingRepository repo) => new(repo);

    private sealed class FakeRepository : IMeetingRepository
    {
        public IReadOnlyList<ParticipantConflict> Conflicts { get; init; } = [];
        public int Calls { get; private set; }
        public IReadOnlyCollection<string>? LastEmails { get; private set; }
        public DateTime LastFrom { get; private set; }
        public DateTime LastTo { get; private set; }
        public Guid? LastExclude { get; private set; }

        public Task<IReadOnlyList<ParticipantConflict>> FindParticipantConflictsAsync(
            IReadOnlyCollection<string> emails, DateTime fromUtc, DateTime toUtc, Guid? excludeMeetingId, CancellationToken cancellationToken)
        {
            Calls++;
            (LastEmails, LastFrom, LastTo, LastExclude) = (emails, fromUtc, toUtc, excludeMeetingId);
            return Task.FromResult(Conflicts);
        }

        public Task AddAsync(Meeting meeting, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Meeting?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> IsCancelledAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResult<Meeting>> SearchAsync(MeetingSearchCriteria criteria, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
