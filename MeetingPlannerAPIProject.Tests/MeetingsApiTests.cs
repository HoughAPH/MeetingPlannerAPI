using MeetingPlannerAPI.Domain.Meetings;
using MeetingPlannerAPI.API.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using System.Net;
using System.Net.Http.Json;

namespace MeetingPlannerAPIProject.Tests;

/// <summary>End-to-end tests against the real pipeline and a throwaway SQLite file.</summary>
public sealed class MeetingsApiTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"meetingplanner-tests-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public MeetingsApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}"));
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateListCancel_RoundTrip()
    {
        MeetingResponse created = await CreateAsync("Consultation", Start, Start.AddMinutes(30), "Jane@Example.com");
        Assert.Equal("jane@example.com", Assert.Single(created.Participants).Email);
        Assert.Equal(ResponseStatus.NoResponse, created.Participants[0].Response);

        PagedResponse<MeetingResponse> listed = await SearchAsync("");
        Assert.Equal(created.Id, Assert.Single(listed.Items).Id);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/meetings/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/meetings/{created.Id}")).StatusCode);
        Assert.Empty((await SearchAsync("")).Items);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/meetings/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Create_WhenEndBeforeStart_Returns400()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/meetings", new
        {
            title = "Bad",
            startTime = Start,
            endTime = Start.AddMinutes(-5)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_UsesOverlapSemantics_ExcludingTouchingBoundaries()
    {
        DateTimeOffset from = Start;
        DateTimeOffset to = Start.AddHours(2);

        await CreateAsync("ends-at-from", from.AddHours(-1), from);
        await CreateAsync("starts-at-to", to, to.AddHours(1));
        await CreateAsync("inside", from.AddMinutes(30), from.AddMinutes(60));
        await CreateAsync("spans-from", from.AddMinutes(-30), from.AddMinutes(30));

        PagedResponse<MeetingResponse> result = await SearchAsync($"?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}");

        Assert.Equal(["spans-from", "inside"], result.Items.Select(e => e.Title));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Search_TextTreatsLikeWildcardsLiterally_AndParticipantEmailIsCaseInsensitive()
    {
        await CreateAsync("100% focus", Start, Start.AddHours(1), "Jane@Example.com");
        await CreateAsync("100 days", Start, Start.AddHours(1));

        Assert.Equal("100% focus", Assert.Single((await SearchAsync("?q=%25")).Items).Title);
        Assert.Equal("100% focus", Assert.Single((await SearchAsync("?participantEmail=JANE@example.com")).Items).Title);
    }

    [Fact]
    public async Task Update_RequiresCurrentETag()
    {
        MeetingResponse created = await CreateAsync("Consultation", Start, Start.AddMinutes(30), "jane@example.com");
        Assert.Equal(1, created.Version);

        HttpResponseMessage ok = await PutAsync(created.Id, "\"1\"", "Checkup");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("\"2\"", ok.Headers.ETag?.Tag);
        Assert.Equal(2, (await ok.Content.ReadFromJsonAsync<MeetingResponse>())!.Version);

        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutAsync(created.Id, "\"1\"", "Stale")).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await PutAsync(created.Id, null, "No header")).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await PutAsync(created.Id, "*", "Wildcard")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(Guid.NewGuid(), "\"1\"", "Missing")).StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsETagForFirstVersion()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/meetings", new
        {
            title = "Consultation",
            startTime = Start,
            endTime = Start.AddMinutes(30)
        });

        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task Create_ReturnsLocationHeaderPointingAtTheMeeting()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/meetings", new
        {
            title = "Consultation",
            startTime = Start,
            endTime = Start.AddMinutes(30)
        });
        MeetingResponse created = (await response.Content.ReadFromJsonAsync<MeetingResponse>())!;

        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/meetings/{created.Id}", response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task GetById_ReturnsMeetingWithParticipantsAndETag()
    {
        MeetingResponse created = await CreateAsync("Consultation", Start, Start.AddMinutes(30), "Jane@Example.com");

        HttpResponseMessage response = await _client.GetAsync($"/api/meetings/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"1\"", response.Headers.ETag?.Tag);
        MeetingResponse fetched = (await response.Content.ReadFromJsonAsync<MeetingResponse>())!;
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Consultation", fetched.Title);
        Assert.Equal(Start, fetched.StartTime);
        Assert.Equal("jane@example.com", Assert.Single(fetched.Participants).Email);
    }

    [Fact]
    public async Task GetById_ETagCanBeUsedAsIfMatchForUpdate()
    {
        MeetingResponse created = await CreateAsync("Consultation", Start, Start.AddMinutes(30));
        string etag = (await _client.GetAsync($"/api/meetings/{created.Id}")).Headers.ETag!.Tag;

        Assert.Equal(HttpStatusCode.OK, (await PutAsync(created.Id, etag, "Renamed")).StatusCode);

        HttpResponseMessage after = await _client.GetAsync($"/api/meetings/{created.Id}");
        Assert.Equal("\"2\"", after.Headers.ETag?.Tag);
        Assert.Equal("Renamed", (await after.Content.ReadFromJsonAsync<MeetingResponse>())!.Title);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundForUnknownMalformedAndCancelledIds()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/meetings/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/meetings/not-a-guid")).StatusCode);

        MeetingResponse created = await CreateAsync("Consultation", Start, Start.AddMinutes(30));
        await _client.DeleteAsync($"/api/meetings/{created.Id}");

        HttpResponseMessage cancelled = await _client.GetAsync($"/api/meetings/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, cancelled.StatusCode);
        Assert.Equal("application/problem+json", cancelled.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Availability_ReportsOverlapsOnly_IgnoringCancelledAndTouchingMeetings()
    {
        await CreateAsync("overlaps", Start, Start.AddHours(1), "Jane@Example.com");
        await CreateAsync("touches", Start.AddHours(1), Start.AddHours(2), "jane@example.com");
        MeetingResponse cancelled = await CreateAsync("cancelled", Start, Start.AddHours(1), "jane@example.com");
        await _client.DeleteAsync($"/api/meetings/{cancelled.Id}");

        IReadOnlyList<ParticipantAvailabilityResponse> result = await CheckAvailabilityAsync(Start.AddMinutes(30), Start.AddHours(1), "JANE@example.com", "free@example.com");

        ParticipantAvailabilityResponse jane = result.Single(r => r.Email == "jane@example.com");
        Assert.False(jane.IsAvailable);
        Assert.Equal("overlaps", Assert.Single(jane.Conflicts).Title);
        Assert.True(result.Single(r => r.Email == "free@example.com").IsAvailable);

        // A window starting exactly when her last event ends is free.
        Assert.True(Assert.Single(await CheckAvailabilityAsync(Start.AddHours(2), Start.AddHours(3), "jane@example.com")).IsAvailable);
    }

    [Fact]
    public async Task Availability_RejectsMissingEmailsAndInvalidWindow()
    {
        string from = Uri.EscapeDataString(Start.ToString("o"));
        string to = Uri.EscapeDataString(Start.AddHours(1).ToString("o"));

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/availability?from={from}&to={to}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/availability?email=a@example.com&from={to}&to={from}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/availability?email=a@example.com")).StatusCode);
    }

    [Fact]
    public async Task CreateAndUpdate_WarnAboutDoubleBookedParticipants_ButStillSucceed()
    {
        MeetingResponse first = await CreateAsync("first", Start, Start.AddHours(1), "jane@example.com");
        Assert.Null(first.AvailabilityConflicts);

        MeetingResponse second = await CreateAsync("second", Start.AddMinutes(10), Start.AddHours(2), "Jane@Example.com");
        AvailabilityConflictResponse conflict = Assert.Single(second.AvailabilityConflicts!);
        Assert.Equal(first.Id, conflict.MeetingId);
        Assert.Equal("jane@example.com", conflict.Email);

        // Updating the first event reports the second, but never itself.
        HttpResponseMessage put = await PutAsync(first.Id, "\"1\"", "first renamed", "jane@example.com");
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        MeetingResponse updated = (await put.Content.ReadFromJsonAsync<MeetingResponse>())!;
        Assert.Equal(second.Id, Assert.Single(updated.AvailabilityConflicts!).MeetingId);
    }

    [Fact]
    public async Task Respond_AcceptThenDecline_UpdatesStatusAndETag()
    {
        MeetingResponse created = await CreateAsync("Consultation", Start, Start.AddMinutes(30), "jane@example.com");
        Guid participantId = created.Participants.Single().Id;

        HttpResponseMessage accepted = await RespondAsync(created.Id, participantId, "Accepted");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("\"2\"", accepted.Headers.ETag?.Tag);
        MeetingResponse body = (await accepted.Content.ReadFromJsonAsync<MeetingResponse>())!;
        Assert.Equal(ResponseStatus.Accepted, body.Participants.Single().Response);

        HttpResponseMessage declined = await RespondAsync(created.Id, participantId, "Declined");
        Assert.Equal(ResponseStatus.Declined, (await declined.Content.ReadFromJsonAsync<MeetingResponse>())!.Participants.Single().Response);

        MeetingResponse fetched = (await _client.GetFromJsonAsync<MeetingResponse>($"/api/meetings/{created.Id}"))!;
        Assert.Equal(ResponseStatus.Declined, fetched.Participants.Single().Response);
        Assert.Equal(3, fetched.Version);
    }

    [Fact]
    public async Task Respond_ReturnsNotFoundForUnknownMeetingOrParticipant_AndBadRequestForBadInput()
    {
        MeetingResponse created = await CreateAsync("Consultation", Start, Start.AddMinutes(30), "jane@example.com");
        Guid participantId = created.Participants.Single().Id;

        Assert.Equal(HttpStatusCode.NotFound, (await RespondAsync(Guid.NewGuid(), participantId, "Accepted")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RespondAsync(created.Id, Guid.NewGuid(), "Accepted")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RespondAsync(created.Id, participantId, "NoResponse")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RespondAsync(created.Id, participantId, "Maybe")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync(
            $"/api/meetings/{created.Id}/participants/{participantId}/response", new { })).StatusCode);

        await _client.DeleteAsync($"/api/meetings/{created.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, (await RespondAsync(created.Id, participantId, "Accepted")).StatusCode);
    }

    [Fact]
    public async Task Respond_AcceptingAnOverlappingMeeting_StillSucceeds_ButWarns()
    {
        MeetingResponse first = await CreateAsync("first", Start, Start.AddHours(1), "jane@example.com");
        MeetingResponse second = await CreateAsync("second", Start.AddMinutes(10), Start.AddHours(2), "jane@example.com", "bob@example.com");
        Guid janeId = second.Participants.Single(p => p.Email == "jane@example.com").Id;

        MeetingResponse accepted = (await (await RespondAsync(second.Id, janeId, "Accepted")).Content.ReadFromJsonAsync<MeetingResponse>())!;

        Assert.Equal(ResponseStatus.Accepted, accepted.Participants.Single(p => p.Id == janeId).Response);
        AvailabilityConflictResponse conflict = Assert.Single(accepted.AvailabilityConflicts!);
        Assert.Equal(first.Id, conflict.MeetingId);
        Assert.Equal("jane@example.com", conflict.Email);

        // Declining never warns.
        MeetingResponse declined = (await (await RespondAsync(second.Id, janeId, "Declined")).Content.ReadFromJsonAsync<MeetingResponse>())!;
        Assert.Null(declined.AvailabilityConflicts);
    }

    [Fact]
    public async Task Availability_IgnoresMeetingsTheParticipantDeclined()
    {
        MeetingResponse declined = await CreateAsync("declined", Start, Start.AddHours(1), "bob@example.com");
        await RespondAsync(declined.Id, declined.Participants.Single().Id, "Declined");

        Assert.True(Assert.Single(await CheckAvailabilityAsync(Start.AddMinutes(30), Start.AddHours(2), "bob@example.com")).IsAvailable);

        await RespondAsync(declined.Id, declined.Participants.Single().Id, "Accepted");

        Assert.False(Assert.Single(await CheckAvailabilityAsync(Start.AddMinutes(30), Start.AddHours(2), "bob@example.com")).IsAvailable);
    }

    [Fact]
    public async Task Search_HugePageNumber_ReturnsAnEmptyPageInsteadOfFailing()
    {
        await CreateAsync("only", Start, Start.AddHours(1));

        PagedResponse<MeetingResponse> result = await SearchAsync($"?page={int.MaxValue}&pageSize=100");

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    private Task<HttpResponseMessage> RespondAsync(Guid meetingId, Guid participantId, string response) =>
        _client.PutAsJsonAsync($"/api/meetings/{meetingId}/participants/{participantId}/response", new { response });

    private async Task<IReadOnlyList<ParticipantAvailabilityResponse>> CheckAvailabilityAsync(
        DateTimeOffset from, DateTimeOffset to, params string[] emails)
    {
        string query = string.Join("&", emails.Select(e => $"email={Uri.EscapeDataString(e)}"))
            + $"&from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}";
        return (await _client.GetFromJsonAsync<List<ParticipantAvailabilityResponse>>($"/api/availability?{query}"))!;
    }

    private Task<HttpResponseMessage> PutAsync(Guid id, string? ifMatch, string title, params string[] emails)
    {
        HttpRequestMessage request = new(HttpMethod.Put, $"/api/meetings/{id}")
        {
            Content = JsonContent.Create(new
            {
                title,
                startTime = Start,
                endTime = Start.AddMinutes(30),
                participants = emails.Select(e => new { name = "Someone", email = e })
            })
        };
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return _client.SendAsync(request);
    }

    private async Task<MeetingResponse> CreateAsync(string title, DateTimeOffset start, DateTimeOffset end, params string[] emails)
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/meetings", new
        {
            title,
            startTime = start,
            endTime = end,
            participants = emails.Select(e => new { name = "Someone", email = e })
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MeetingResponse>())!;
    }

    private async Task<PagedResponse<MeetingResponse>> SearchAsync(string query) =>
        (await _client.GetFromJsonAsync<PagedResponse<MeetingResponse>>($"/api/meetings{query}"))!;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}
