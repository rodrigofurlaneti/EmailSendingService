namespace EmailSendingService.Domain.Exceptions;

public sealed class EmailMessageValidationException : DomainException
{
    public EmailMessageValidationException(string message) : base(message) { }
}
