using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Application.Interfaces;
using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Application.Meetings.CancelMeeting;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPIProject.Tests;

public class CancelMeetingHandlerTests
{
    private static readonly DateTime Start = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Cancel_NotifiesParticipantsOnce_EvenWhenCalledTwice()
    {
        Meeting @meeting = Meeting.Create("Meeting", null, Start, Start.AddHours(1),
            [new NewParticipant("Jane", "Jane@Example.com")]);
        FakeUnitOfWork uow = new();
        FakeNotifier notifier = new();
        CancelMeetingHandler handler = new(new FakeRepository(@meeting), uow, notifier);
        CancelMeetingCommand command = new(@meeting.Id);

        Assert.True(await handler.HandleAsync(command, default));
        Assert.True(await handler.HandleAsync(command, default));

        Assert.True(@meeting.IsCancelled);
        Assert.Equal(1, uow.SaveCount);
        Notification sent = Assert.Single(notifier.Sent);
        Assert.Equal(NotificationType.MeetingCancelled, sent.Type);
        Assert.Equal(@meeting.Id, sent.MeetingId);
        Assert.Equal(["jane@example.com"], sent.RecipientEmails);
    }

    [Fact]
    public async Task Cancel_UnknownId_ReturnsFalseAndSendsNothing()
    {
        FakeUnitOfWork uow = new();
        FakeNotifier notifier = new();
        CancelMeetingHandler handler = new(new FakeRepository(), uow, notifier);

        Assert.False(await handler.HandleAsync(new CancelMeetingCommand(Guid.NewGuid()), default));

        Assert.Equal(0, uow.SaveCount);
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task Cancel_LosingRaceToAnotherCancel_ReturnsTrueAndSendsNothing()
    {
        Meeting @meeting = Meeting.Create("Meeting", null, Start, Start.AddHours(1), [new NewParticipant("Jane", "jane@example.com")]);
        FakeUnitOfWork uow = new() { ThrowConflict = true };
        FakeNotifier notifier = new();
        CancelMeetingHandler handler = new(new FakeRepository(@meeting) { CancelledInStore = true }, uow, notifier);

        Assert.True(await handler.HandleAsync(new CancelMeetingCommand(@meeting.Id), default));
        Assert.Empty(notifier.Sent);
    }

    [Fact]
    public async Task Cancel_LosingRaceToAnUpdate_RethrowsConflict()
    {
        Meeting @meeting = Meeting.Create("Meeting", null, Start, Start.AddHours(1), []);
        FakeNotifier notifier = new();
        CancelMeetingHandler handler = new(new FakeRepository(@meeting), new FakeUnitOfWork { ThrowConflict = true }, notifier);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => handler.HandleAsync(new CancelMeetingCommand(@meeting.Id), default));
        Assert.Empty(notifier.Sent);
    }

    private sealed class FakeRepository(params Meeting[] meetings) : IMeetingRepository
    {
        public bool CancelledInStore { get; init; }

        public Task AddAsync(Meeting @meeting, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Meeting?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(meetings.FirstOrDefault(e => e.Id == id));

        public Task<bool> IsCancelledAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(CancelledInStore);

        public Task<PagedResult<Meeting>> SearchAsync(MeetingSearchCriteria criteria, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

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
