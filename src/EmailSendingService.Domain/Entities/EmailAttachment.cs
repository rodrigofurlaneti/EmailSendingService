using EmailSendingService.Domain.Common;
using EmailSendingService.Domain.Exceptions;

namespace EmailSendingService.Domain.Entities;

/// <summary>An in-memory attachment carried by an <see cref="EmailMessage"/>.</summary>
public sealed class EmailAttachment : ValueObject
{
    public string FileName { get; }
    public string ContentType { get; }
    public byte[] Content { get; }

    private EmailAttachment(string fileName, string contentType, byte[] content)
    {
        FileName = fileName;
        ContentType = contentType;
        Content = content;
    }

    public static EmailAttachment Create(string fileName, string contentType, byte[] content)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new EmailMessageValidationException("Attachment file name is required.");
        if (content is null || content.Length == 0)
            throw new EmailMessageValidationException($"Attachment '{fileName}' has no content.");

        var type = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();

        return new EmailAttachment(fileName.Trim(), type, content);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FileName;
        yield return ContentType;
        yield return Convert.ToBase64String(Content);
    }
}
