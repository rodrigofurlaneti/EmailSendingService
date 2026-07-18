namespace EmailSendingService.Application.Emails.Dtos;

/// <summary>
/// The DTO received by the API. Kept intentionally free of domain rules — it is
/// just a data carrier that the Application layer maps into the domain model.
/// </summary>
public sealed class SendEmailRequest
{
    public RecipientDto? From { get; set; }
    public List<RecipientDto> To { get; set; } = new();
    public List<RecipientDto> Cc { get; set; } = new();
    public List<RecipientDto> Bcc { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsHtml { get; set; }
    public List<AttachmentDto> Attachments { get; set; } = new();
}
