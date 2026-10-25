using MeetingPlannerAPI.Domain.Common;
using MeetingPlannerAPI.Domain.Meetings;

namespace MeetingPlannerAPIProject.Tests;

public class MeetingDomainTests
{
    private static readonly DateTime Start = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidData_ReturnsMeetingWithNormalisedValues()
    {
        Meeting @meeting = Meeting.Create(
            "  Initial consultation  ",
            "Optional text",
            Start,
            Start.AddMinutes(30),
            [new NewParticipant("Jane Doe", "Jane@Example.com")]);

        Assert.NotEqual(Guid.Empty, @meeting.Id);
        Assert.Equal("Initial consultation", @meeting.Title);
        Assert.Equal(Start, @meeting.StartTimeUtc);
        Assert.False(@meeting.IsCancelled);

        Participant participant = Assert.Single(@meeting.Participants);
        Assert.Equal("jane@example.com", participant.Email);
        Assert.False(participant.IsAttending);
    }

    [Fact]
    public void Create_WhenEndIsBeforeStart_ThrowsInvalidRange()
    {
        DomainException ex = Assert.Throws<DomainException>(() =>
            Meeting.Create("Meeting", null, Start, Start.AddMinutes(-30), null));

        Assert.Equal("meeting.time.invalid_range", ex.Code);
    }

    [Fact]
    public void Create_WithDuplicateParticipantEmailIgnoringCase_ThrowsDuplicate()
    {
        DomainException ex = Assert.Throws<DomainException>(() =>
            Meeting.Create("Meeting", null, Start, Start.AddHours(1),
            [
                new NewParticipant("Jane", "jane@example.com"),
                new NewParticipant("Jane Again", "JANE@example.com")
            ]));

        Assert.Equal("meeting.participants.duplicate", ex.Code);
    }

    [Fact]
    public void Create_WhenEndEqualsStart_ThrowsInvalidRange()
    {
        DomainException ex = Assert.Throws<DomainException>(() =>
            Meeting.Create("Meeting", null, Start, Start, null));

        Assert.Equal("meeting.time.invalid_range", ex.Code);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Create_WhenTimesAreNotUtc_ThrowsNotUtc(DateTimeKind kind)
    {
        DateTime start = DateTime.SpecifyKind(Start, kind);

        DomainException ex = Assert.Throws<DomainException>(() =>
            Meeting.Create("Meeting", null, start, start.AddHours(1), null));

        Assert.Equal("meeting.time.not_utc", ex.Code);
    }

    [Fact]
    public void Create_AcceptsExactlyMaxParticipants_ButRejectsOneMore()
    {
        static List<NewParticipant> Participants(int count) =>
            Enumerable.Range(0, count).Select(i => new NewParticipant($"Person {i}", $"p{i}@example.com")).ToList();

        Meeting full = Meeting.Create("Meeting", null, Start, Start.AddHours(1), Participants(MeetingLimits.MaxParticipants));
        Assert.Equal(MeetingLimits.MaxParticipants, full.Participants.Count);

        DomainException ex = Assert.Throws<DomainException>(() =>
            Meeting.Create("Meeting", null, Start, Start.AddHours(1), Participants(MeetingLimits.MaxParticipants + 1)));
        Assert.Equal("meeting.participants.too_many", ex.Code);
    }

    [Fact]
    public void Cancel_FirstCallChangesState_SecondCallDoesNot()
    {
        Meeting @meeting = Meeting.Create("Meeting", null, Start, Start.AddHours(1), null);

        Assert.True(@meeting.Cancel());
        Assert.True(@meeting.IsCancelled);
        DateTime? cancelledAt = @meeting.CancelledAtUtc;

        Assert.False(@meeting.Cancel());
        Assert.Equal(cancelledAt, @meeting.CancelledAtUtc);
    }

    [Fact]
    public void Version_StartsAtOne_AndIncrementsOnUpdateAndCancel()
    {
        Meeting @meeting = Meeting.Create("Meeting", null, Start, Start.AddHours(1), []);
        Assert.Equal(1, @meeting.Version);

        @meeting.Update("Meeting", null, Start, Start.AddHours(2), []);
        Assert.Equal(2, @meeting.Version);

        Assert.True(@meeting.Cancel());
        Assert.False(@meeting.Cancel());
        Assert.Equal(3, @meeting.Version);
    }

    [Fact]
    public void Update_ParticipantOnlyChange_IncrementsVersion_AndKeepsExistingParticipants()
    {
        Meeting @meeting = Meeting.Create("Meeting", null, Start, Start.AddHours(1),
            [new NewParticipant("Jane", "jane@example.com"), new NewParticipant("Bob", "bob@example.com")]);
        Guid janeId = @meeting.Participants.Single(a => a.Email == "jane@example.com").Id;

        @meeting.Update("Meeting", null, Start, Start.AddHours(1),
            [new NewParticipant("Jane D.", "JANE@example.com"), new NewParticipant("Ann", "ann@example.com")]);

        Assert.Equal(2, @meeting.Version);
        Assert.Equal(["ann@example.com", "jane@example.com"], @meeting.Participants.Select(a => a.Email).Order());
        Participant jane = @meeting.Participants.Single(a => a.Email == "jane@example.com");
        Assert.Equal(janeId, jane.Id);
        Assert.Equal("Jane D.", jane.Name);
    }

    [Fact]
    public void Update_InvalidInput_LeavesMeetingUnchanged()
    {
        Meeting @meeting = Meeting.Create("Meeting", null, Start, Start.AddHours(1), [new NewParticipant("Jane", "jane@example.com")]);

        DomainException ex = Assert.Throws<DomainException>(() => @meeting.Update("New title", null, Start, Start.AddHours(1),
            [new NewParticipant("A", "dup@example.com"), new NewParticipant("B", "DUP@example.com")]));

        Assert.Equal("meeting.participants.duplicate", ex.Code);
        Assert.Equal("Meeting", @meeting.Title);
        Assert.Equal(1, @meeting.Version);
        Assert.Equal("jane@example.com", Assert.Single(@meeting.Participants).Email);
    }

    [Fact]
    public void Update_CancelledMeeting_Throws()
    {
        Meeting @meeting = Meeting.Create("Meeting", null, Start, Start.AddHours(1), []);
        @meeting.Cancel();

        DomainException ex = Assert.Throws<DomainException>(() => @meeting.Update("Meeting", null, Start, Start.AddHours(1), []));
        Assert.Equal("meeting.cancelled", ex.Code);
    }
}
