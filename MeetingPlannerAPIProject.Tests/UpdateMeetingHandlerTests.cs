using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Application.Interfaces;
using MeetingPlannerAPI.Application.Meetings;
using MeetingPlannerAPI.Application.Meetings.UpdateMeeting;
using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPIProject.Tests;

public class UpdateMeetingHandlerTests
{
    private static readonly DateTime Start = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static Meeting NewMeeting(params NewParticipant[] participants) =>
        Meeting.Create("Meeting", null, Start, Start.AddHours(1), participants);

    private static UpdateMeetingCommand Command(Meeting meeting, int? version = null, string title = "Renamed", params NewParticipant[] participants) =>
        new(meeting.Id, version ?? meeting.Version, title, "Notes", Start, Start.AddHours(2), participants);

    [Fact]
    public async Task Update_AppliesChanges_SavesOnce_AndNotifiesCurrentParticipants()
    {
        Meeting meeting = NewMeeting(new NewParticipant("Jane", "jane@example.com"));
        FakeUnitOfWork uow = new();
        FakeNotifier notifier = new();
        UpdateMeetingHandler handler = new(new FakeRepository(meeting), uow, notifier);

        MeetingDto? dto = await handler.HandleAsync(
            Command(meeting, 1, "Renamed", new NewParticipant("Bob", "Bob@Example.com")), default);

        Assert.NotNull(dto);
        Assert.Equal("Renamed", dto.Title);
        Assert.Equal("Notes", dto.Description);
        Assert.Equal(Start.AddHours(2), dto.EndTimeUtc);
        Assert.Equal(2, dto.Version);
        Assert.Equal("bob@example.com", Assert.Single(dto.Participants).Email);
        Assert.Equal(1, uow.SaveCount);

        Notification sent = Assert.Single(notifier.Sent);
        Assert.Equal(NotificationType.MeetingUpdated, sent.Type);
        Assert.Equal(meeting.Id, sent.MeetingId);
        Assert.Equal("Renamed", sent.Title);
        Assert.Equal(["bob@example.com"], sent.RecipientEmails);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNullAndDoesNothing()
    {
        FakeUnitOfWork uow = new();
        FakeNotifier notifier = new();
        UpdateMeetingHandler handler = new(new FakeRepository(), uow, notifier);

        MeetingDto? dto = await handler.HandleAsync(
            new UpdateMeetingCommand(Guid.NewGuid(), 1, "Title", null, Start, Start.AddHours(1), []), default);

        Assert.Null(dto);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task Update_StaleVersion_ThrowsConflict_WithoutChangingOrSavingOrNotifying()
    {
        Meeting meeting = NewMeeting();
        meeting.Update("Meeting", null, Start, Start.AddHours(1), []); // now version 2
        FakeUnitOfWork uow = new();
        FakeNotifier notifier = new();
        UpdateMeetingHandler handler = new(new FakeRepository(meeting), uow, notifier);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => handler.HandleAsync(Command(meeting, 1), default));

        Assert.Equal("Meeting", meeting.Title);
        Assert.Equal(2, meeting.Version);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task Update_LosingRaceAtSave_PropagatesConflict_AndDoesNotNotify()
    {
        Meeting meeting = NewMeeting();
        FakeNotifier notifier = new();
        UpdateMeetingHandler handler = new(new FakeRepository(meeting), new FakeUnitOfWork { ThrowConflict = true }, notifier);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => handler.HandleAsync(Command(meeting), default));

        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task Update_InvalidInput_ThrowsDomainException_WithoutSavingOrNotifying()
    {
        Meeting meeting = NewMeeting(new NewParticipant("Jane", "jane@example.com"));
        FakeUnitOfWork uow = new();
        FakeNotifier notifier = new();
        UpdateMeetingHandler handler = new(new FakeRepository(meeting), uow, notifier);

        DomainException ex = await Assert.ThrowsAsync<DomainException>(
            () => handler.HandleAsync(Command(meeting, title: "  "), default));

        Assert.Equal("meeting.title.required", ex.Code);
        Assert.Equal("Meeting", meeting.Title);
        Assert.Equal(1, meeting.Version);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task Update_CancelledMeeting_ThrowsDomainException_AndSendsNothing()
    {
        Meeting meeting = NewMeeting();
        meeting.Cancel();
        FakeUnitOfWork uow = new();
        FakeNotifier notifier = new();
        UpdateMeetingHandler handler = new(new FakeRepository(meeting), uow, notifier);

        DomainException ex = await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(Command(meeting), default));

        Assert.Equal("meeting.cancelled", ex.Code);
        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task Update_KeepsExistingParticipantIdentity_WhenEmailMatchesIgnoringCase()
    {
        Meeting meeting = NewMeeting(new NewParticipant("Jane", "jane@example.com"));
        Guid janeId = meeting.Participants.Single().Id;
        UpdateMeetingHandler handler = new(new FakeRepository(meeting), new FakeUnitOfWork(), new FakeNotifier());

        MeetingDto? dto = await handler.HandleAsync(
            Command(meeting, participants: new NewParticipant("Jane D.", "JANE@example.com")), default);

        ParticipantDto jane = Assert.Single(dto!.Participants);
        Assert.Equal(janeId, jane.Id);
        Assert.Equal("Jane D.", jane.Name);
    }

    private sealed class FakeRepository(params Meeting[] meetings) : IMeetingRepository
    {
        public Task<Meeting?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(meetings.FirstOrDefault(m => m.Id == id));

        public Task AddAsync(Meeting meeting, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> IsCancelledAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResult<Meeting>> SearchAsync(MeetingSearchCriteria criteria, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<ParticipantConflict>> FindParticipantConflictsAsync(
            IReadOnlyCollection<string> emails, DateTime fromUtc, DateTime toUtc, Guid? excludeMeetingId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public bool ThrowConflict { get; init; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            if (ThrowConflict)
                throw new ConcurrencyConflictException("conflict");
            return Task.FromResult(1);
        }
    }

    private sealed class FakeNotifier : INotificationService
    {
        public List<Notification> Sent { get; } = [];

        public Task SendAsync(Notification notification, CancellationToken cancellationToken)
        {
            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }
}
