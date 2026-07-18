namespace EmailSendingService.Domain.Exceptions;

/// <summary>
/// Base type for all domain rule violations. Signals that an invariant of the
/// domain model was broken (as opposed to an infrastructure/transport failure).
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
