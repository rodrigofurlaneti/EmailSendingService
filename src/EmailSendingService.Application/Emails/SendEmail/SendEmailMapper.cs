using EmailSendingService.Application.Emails.Dtos;
using EmailSendingService.Domain.Entities;
using EmailSendingService.Domain.Exceptions;
using EmailSendingService.Domain.ValueObjects;

namespace EmailSendingService.Application.Emails.SendEmail;

/// <summary>Translates the transport DTO into a validated domain aggregate.</summary>
public static class SendEmailMapper
{
    public static EmailMessage ToDomain(SendEmailRequest request, string? defaultFromAddress, string? defaultFromName)
    {
        ArgumentNullException.ThrowIfNull(request);

        var from = ResolveSender(request.From, defaultFromAddress, defaultFromName);

        var to = request.To.Select(ToAddress).ToList();
        var cc = request.Cc.Select(ToAddress).ToList();
        var bcc = request.Bcc.Select(ToAddress).ToList();

        // Placeholder attachment entries (all fields blank) are ignored so that a
        // caller sending an empty "attachments": [{}] slot is not rejected.
        var attachments = request.Attachments
            .Where(a => !IsEmpty(a))
            .Select(ToAttachment)
            .ToList();

        var format = request.IsHtml ? EmailBodyFormat.Html : EmailBodyFormat.PlainText;

        return EmailMessage.Create(
            from: from,
            to: to,
            subject: request.Subject,
            body: request.Body,
            bodyFormat: format,
            cc: cc,
            bcc: bcc,
            attachments: attachments);
    }

    private static EmailAddress ResolveSender(RecipientDto? from, string? defaultAddress, string? defaultName)
    {
        if (from is not null && !string.IsNullOrWhiteSpace(from.Address))
            return EmailAddress.Create(from.Address, from.Name);

        if (!string.IsNullOrWhiteSpace(defaultAddress))
            return EmailAddress.Create(defaultAddress, defaultName);

        throw new EmailMessageValidationException(
            "No sender address was provided and no default 'From' is configured.");
    }

    private static EmailAddress ToAddress(RecipientDto dto)
        => EmailAddress.Create(dto.Address, dto.Name);

    private static bool IsEmpty(AttachmentDto dto)
        => string.IsNullOrWhiteSpace(dto.FileName)
           && string.IsNullOrWhiteSpace(dto.ContentBase64)
           && string.IsNullOrWhiteSpace(dto.ContentType);

    private static EmailAttachment ToAttachment(AttachmentDto dto)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(dto.ContentBase64 ?? string.Empty);
        }
        catch (FormatException)
        {
            throw new EmailMessageValidationException(
                $"Attachment '{dto.FileName}' has invalid Base64 content.");
        }

        return EmailAttachment.Create(dto.FileName, dto.ContentType, bytes);
    }
}
