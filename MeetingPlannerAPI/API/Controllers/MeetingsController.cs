using MeetingPlannerAPI.API.Contracts;
using MeetingPlannerAPI.Application.Meetings;
using MeetingPlannerAPI.Application.Meetings.CancelMeeting;
using MeetingPlannerAPI.Application.Meetings.CheckAvailability;
using MeetingPlannerAPI.Application.Meetings.CreateMeeting;
using MeetingPlannerAPI.Application.Meetings.GetMeeting;
using MeetingPlannerAPI.Application.Meetings.SearchMeetings;
using MeetingPlannerAPI.Application.Meetings.UpdateMeeting;
using MeetingPlannerAPI.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace MeetingPlannerAPI.API.Controllers;

[ApiController]
[Route("api/meetings")]
public sealed class MeetingsController : ControllerBase
{
    /// <summary>Creates a meeting with its initial participants. Participants already booked on overlapping meetings are reported in <c>availabilityConflicts</c>; the meeting is still created.</summary>
    [HttpPost]
    [ProducesResponseType<MeetingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MeetingResponse>> Create(
        CreateMeetingRequest request,
        [FromServices] CreateMeetingHandler handler,
        [FromServices] CheckAvailabilityHandler availability,
        CancellationToken cancellationToken)
    {
        MeetingDto created = await handler.HandleAsync(request.ToCommand(), cancellationToken);

        Response.Headers.ETag = ETags.Format(created.Version);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            created.ToResponse(await FindConflictsAsync(created, availability, cancellationToken)));
    }

    /// <summary>Gets a single meeting with its participants. The ETag header carries the version to send back in If-Match when updating.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<MeetingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MeetingResponse>> GetById(
        Guid id,
        [FromServices] GetMeetingHandler handler,
        CancellationToken cancellationToken)
    {
        MeetingDto? found = await handler.HandleAsync(id, cancellationToken);
        if (found is null)
            return NotFound();

        Response.Headers.ETag = ETags.Format(found.Version);
        return Ok(found.ToResponse());
    }

    /// <summary>Replaces a meeting's details and participants. Requires If-Match with the meeting's current ETag.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<MeetingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<MeetingResponse>> Update(
        Guid id,
        UpdateMeetingRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] UpdateMeetingHandler handler,
        [FromServices] CheckAvailabilityHandler availability,
        CancellationToken cancellationToken)
    {
        if (!ETags.TryParse(ifMatch, out int expectedVersion))
            return Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                detail: "An If-Match header containing the meeting's current ETag is required.");

        MeetingDto? updated = await handler.HandleAsync(request.ToCommand(id, expectedVersion), cancellationToken);
        if (updated is null)
            return NotFound();

        Response.Headers.ETag = ETags.Format(updated.Version);
        return Ok(updated.ToResponse(await FindConflictsAsync(updated, availability, cancellationToken)));
    }

    /// <summary>Cancels a meeting and notifies its participants. Repeating the call for a cancelled meeting is a no-op.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromServices] CancelMeetingHandler handler,
        CancellationToken cancellationToken)
    {
        bool found = await handler.HandleAsync(new CancelMeetingCommand(id), cancellationToken);
        return found ? NoContent() : NotFound();
    }

    /// <summary>Lists calendar meetings, optionally filtered by time range or participant, and searched by text.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<MeetingResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<MeetingResponse>>> Search(
        [FromQuery] SearchMeetingsRequest request,
        [FromServices] SearchMeetingsHandler handler,
        CancellationToken cancellationToken)
    {
        PagedResult<MeetingDto> result = await handler.HandleAsync(request.ToQuery(), cancellationToken);
        return Ok(result.ToResponse());
    }

    // Warn-only: the meeting is already saved; this just reports participants double-booked elsewhere.
    private static async Task<IReadOnlyList<ParticipantConflictDto>> FindConflictsAsync(
        MeetingDto saved,
        CheckAvailabilityHandler availability,
        CancellationToken cancellationToken)
    {
        if (saved.Participants.Count == 0)
            return [];

        IReadOnlyList<ParticipantAvailabilityDto> result = await availability.HandleAsync(
            new CheckAvailabilityQuery(
                [.. saved.Participants.Select(a => a.Email)],
                saved.StartTimeUtc,
                saved.EndTimeUtc,
                saved.Id),
            cancellationToken);

        return [.. result.SelectMany(r => r.Conflicts)];
    }
}
