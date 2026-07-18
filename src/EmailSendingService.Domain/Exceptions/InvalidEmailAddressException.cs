namespace EmailSendingService.Domain.Exceptions;

public sealed class InvalidEmailAddressException : DomainException
{
    public InvalidEmailAddressException(string value)
        : base($"'{value}' is not a valid e-mail address.") { }
}
