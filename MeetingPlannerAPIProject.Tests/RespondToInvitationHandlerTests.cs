using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Application.Meetings;
using MeetingPlannerAPI.Application.Meetings.RespondToInvitation;
using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPIProject.Tests;

public class RespondToInvitationHandlerTests
{
    private static readonly DateTime Start = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private static Meeting NewMeeting() =>
        Meeting.Create("Meeting", null, Start, Start.AddHours(1), [new NewParticipant("Jane", "jane@example.com")]);

    [Fact]
    public async Task Respond_SavesOnce_AndReturnsTheUpdatedMeeting()
    {
        Meeting meeting = NewMeeting();
        FakeUnitOfWork uow = new();

        MeetingDto? dto = await new RespondToInvitationHandler(new FakeRepository(meeting), uow).HandleAsync(
            new RespondToInvitationCommand(meeting.Id, meeting.Participants.Single().Id, ResponseStatus.Accepted), default);

        Assert.Equal(ResponseStatus.Accepted, Assert.Single(dto!.Participants).Response);
        Assert.Equal(2, dto.Version);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task Respond_SameAnswerTwice_DoesNotSaveAgain()
    {
        Meeting meeting = NewMeeting();
        FakeUnitOfWork uow = new();
        RespondToInvitationHandler handler = new(new FakeRepository(meeting), uow);
        RespondToInvitationCommand command = new(meeting.Id, meeting.Participants.Single().Id, ResponseStatus.Declined);

        await handler.HandleAsync(command, default);
        MeetingDto? second = await handler.HandleAsync(command, default);

        Assert.Equal(2, second!.Version);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task Respond_UnknownMeetingOrParticipant_ReturnsNull()
    {
        Meeting meeting = NewMeeting();
        FakeUnitOfWork uow = new();
        RespondToInvitationHandler handler = new(new FakeRepository(meeting), uow);

        Assert.Null(await handler.HandleAsync(
            new RespondToInvitationCommand(Guid.NewGuid(), Guid.NewGuid(), ResponseStatus.Accepted), default));
        Assert.Null(await handler.HandleAsync(
            new RespondToInvitationCommand(meeting.Id, Guid.NewGuid(), ResponseStatus.Accepted), default));
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task Respond_LosingRaceAtSave_PropagatesConflict()
    {
        Meeting meeting = NewMeeting();
        RespondToInvitationHandler handler = new(new FakeRepository(meeting), new FakeUnitOfWork { ThrowConflict = true });

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => handler.HandleAsync(
            new RespondToInvitationCommand(meeting.Id, meeting.Participants.Single().Id, ResponseStatus.Accepted), default));
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
}
