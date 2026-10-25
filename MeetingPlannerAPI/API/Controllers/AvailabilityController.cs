using MeetingPlannerAPI.API.Contracts;
using MeetingPlannerAPI.Application.Meetings.CheckAvailability;
using Microsoft.AspNetCore.Mvc;

namespace MeetingPlannerAPI.API.Controllers;

[ApiController]
[Route("api/availability")]
public sealed class AvailabilityController : ControllerBase
{
    /// <summary>
    /// Checks whether participants are free in a time window. A participant is busy if they are on another
    /// non-cancelled meeting that overlaps the window. Advisory only: creating overlapping meetings is still allowed.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ParticipantAvailabilityResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ParticipantAvailabilityResponse>>> Check(
        [FromQuery] AvailabilityRequest request,
        [FromServices] CheckAvailabilityHandler handler,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ParticipantAvailabilityDto> result = await handler.HandleAsync(request.ToQuery(), cancellationToken);
        return Ok(result.Select(r => r.ToResponse()).ToList());
    }
}
