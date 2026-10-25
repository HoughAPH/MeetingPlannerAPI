using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Domain.Meetings;
using MeetingPlannerAPI.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MeetingPlannerAPIProject.Tests;

/// <summary>Two DbContexts on one SQLite file reproduce the simultaneous-request race, which an HTTP test cannot time reliably.</summary>
public sealed class MeetingConcurrencyTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"meetingplanner-concurrency-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task SecondOfTwoSimultaneousSaves_IsRejected_EvenForParticipantOnlyChange()
    {
        Guid id;
        await using (AppDbContext setup = CreateContext())
        {
            await setup.Database.MigrateAsync();
            Meeting created = Meeting.Create("Meeting", null, Start, Start.AddHours(1), []);
            setup.Meetings.Add(created);
            await setup.SaveChangesAsync();
            id = created.Id;
        }

        await using AppDbContext first = CreateContext();
        await using AppDbContext second = CreateContext();
        Meeting a = await first.Meetings.Include(e => e.Participants).SingleAsync(e => e.Id == id);
        Meeting b = await second.Meetings.Include(e => e.Participants).SingleAsync(e => e.Id == id);

        a.Update("Meeting", null, Start, Start.AddHours(1), [new NewParticipant("Jane", "jane@example.com")]);
        await first.SaveChangesAsync();

        b.Update("Meeting", null, Start, Start.AddHours(1), [new NewParticipant("Bob", "bob@example.com")]);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());

        await using AppDbContext check = CreateContext();
        Meeting stored = await check.Meetings.Include(e => e.Participants).SingleAsync(e => e.Id == id);
        Assert.Equal(2, stored.Version);
        Assert.Equal("jane@example.com", Assert.Single(stored.Participants).Email);
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_dbPath}").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}
