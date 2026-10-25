namespace MeetingPlannerAPI.Application.Abstractions;

/// <summary>Raised when a use-case input is invalid for reasons that are not domain invariants (e.g. an inverted date filter).</summary>
public sealed class RequestValidationException : Exception
{
    public RequestValidationException(string message) : base(message)
    {
    }
}
