namespace MeetingPlannerAPI.Domain.Common;

/// <summary>
/// Raised when a domain invariant is violated. <see cref="Code"/> is a stable, machine-readable identifier.
/// </summary>
public sealed class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}
