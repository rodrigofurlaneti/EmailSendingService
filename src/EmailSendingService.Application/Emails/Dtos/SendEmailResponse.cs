namespace EmailSendingService.Application.Emails.Dtos;

public sealed class SendEmailResponse
{
    public Guid EmailId { get; set; }
    public string ProviderMessageId { get; set; } = string.Empty;
    public bool Delivered { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
}
