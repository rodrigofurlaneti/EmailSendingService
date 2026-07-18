namespace EmailSendingService.Application.Emails.Dtos;

/// <summary>Attachment received from the API caller (content is Base64 encoded).</summary>
public sealed class AttachmentDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string ContentBase64 { get; set; } = string.Empty;
}
