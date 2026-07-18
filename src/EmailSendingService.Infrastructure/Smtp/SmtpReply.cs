namespace EmailSendingService.Infrastructure.Smtp;

/// <summary>A parsed SMTP server reply (possibly multi-line).</summary>
public sealed record SmtpReply(int Code, IReadOnlyList<string> Lines)
{
    public bool IsPositiveCompletion => Code is >= 200 and < 300;
    public bool IsPositiveIntermediate => Code is >= 300 and < 400;
    public string Text => string.Join("\n", Lines);

    public override string ToString() => $"{Code} {Text}";
}
