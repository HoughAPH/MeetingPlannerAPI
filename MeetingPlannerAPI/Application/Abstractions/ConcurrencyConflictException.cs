namespace MeetingPlannerAPI.Application.Abstractions;

/// <summary>Raised when a write is based on a version of an aggregate that is no longer current.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
