using MeetingPlannerAPI.Application.Abstractions;
using MeetingPlannerAPI.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MeetingPlannerAPI.API.ErrorHandling;

/// <summary>Maps domain and request validation failures to 400 and concurrency conflicts to 412 problem details; anything else falls through to the default 500 handling.</summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string? code, string? detail) = exception switch
        {
            DomainException e => (StatusCodes.Status400BadRequest, e.Code, e.Message),
            RequestValidationException e => (StatusCodes.Status400BadRequest, "request.invalid", e.Message),
            ConcurrencyConflictException e => (StatusCodes.Status412PreconditionFailed, "meeting.concurrency_conflict", e.Message),
            _ => (0, (string?)null, (string?)null)
        };

        if (detail is null)
            return false;

        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = "The request could not be processed.",
                Detail = detail,
                Extensions = { ["code"] = code }
            }
        });
    }
}
