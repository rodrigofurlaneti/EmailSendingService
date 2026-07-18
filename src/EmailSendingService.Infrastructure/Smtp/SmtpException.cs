namespace EmailSendingService.Infrastructure.Smtp;

/// <summary>Raised when the SMTP conversation fails (unexpected reply code, transport error).</summary>
public sealed class SmtpException : Exception
{
    public int? ReplyCode { get; }

    public SmtpException(string message, int? replyCode = null, Exception? inner = null)
        : base(message, inner)
    {
        ReplyCode = replyCode;
    }
}
