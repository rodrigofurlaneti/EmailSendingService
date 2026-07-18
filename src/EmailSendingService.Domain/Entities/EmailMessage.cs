using EmailSendingService.Domain.Exceptions;
using EmailSendingService.Domain.ValueObjects;

namespace EmailSendingService.Domain.Entities;

/// <summary>
/// Aggregate root that represents an e-mail ready to be dispatched. All
/// invariants (valid sender, at least one recipient, subject/body present)
/// are enforced here, so an <see cref="EmailMessage"/> is always in a
/// consistent, sendable state.
/// </summary>
public sealed class EmailMessage
{
    private readonly List<EmailAddress> _to;
    private readonly List<EmailAddress> _cc;
    private readonly List<EmailAddress> _bcc;
    private readonly List<EmailAttachment> _attachments;

    public Guid Id { get; }
    public EmailAddress From { get; }
    public IReadOnlyCollection<EmailAddress> To => _to.AsReadOnly();
    public IReadOnlyCollection<EmailAddress> Cc => _cc.AsReadOnly();
    public IReadOnlyCollection<EmailAddress> Bcc => _bcc.AsReadOnly();
    public string Subject { get; }
    public string Body { get; }
    public EmailBodyFormat BodyFormat { get; }
    public IReadOnlyCollection<EmailAttachment> Attachments => _attachments.AsReadOnly();
    public DateTimeOffset CreatedAt { get; }

    private EmailMessage(
        Guid id,
        EmailAddress from,
        List<EmailAddress> to,
        List<EmailAddress> cc,
        List<EmailAddress> bcc,
        string subject,
        string body,
        EmailBodyFormat bodyFormat,
        List<EmailAttachment> attachments,
        DateTimeOffset createdAt)
    {
        Id = id;
        From = from;
        _to = to;
        _cc = cc;
        _bcc = bcc;
        Subject = subject;
        Body = body;
        BodyFormat = bodyFormat;
        _attachments = attachments;
        CreatedAt = createdAt;
    }

    public static EmailMessage Create(
        EmailAddress from,
        IEnumerable<EmailAddress> to,
        string subject,
        string body,
        EmailBodyFormat bodyFormat = EmailBodyFormat.PlainText,
        IEnumerable<EmailAddress>? cc = null,
        IEnumerable<EmailAddress>? bcc = null,
        IEnumerable<EmailAttachment>? attachments = null)
    {
        ArgumentNullException.ThrowIfNull(from);

        var toList = (to ?? Enumerable.Empty<EmailAddress>()).ToList();
        var ccList = (cc ?? Enumerable.Empty<EmailAddress>()).ToList();
        var bccList = (bcc ?? Enumerable.Empty<EmailAddress>()).ToList();
        var attachmentList = (attachments ?? Enumerable.Empty<EmailAttachment>()).ToList();

        if (toList.Count == 0 && ccList.Count == 0 && bccList.Count == 0)
            throw new EmailMessageValidationException(
                "An e-mail must have at least one recipient (To, Cc or Bcc).");

        if (string.IsNullOrWhiteSpace(subject))
            throw new EmailMessageValidationException("Subject is required.");

        if (string.IsNullOrWhiteSpace(body))
            throw new EmailMessageValidationException("Body is required.");

        return new EmailMessage(
            Guid.NewGuid(),
            from,
            toList,
            ccList,
            bccList,
            subject.Trim(),
            body,
            bodyFormat,
            attachmentList,
            DateTimeOffset.UtcNow);
    }

    /// <summary>All recipients that must receive the message (To + Cc + Bcc).</summary>
    public IReadOnlyCollection<EmailAddress> AllRecipients()
        => _to.Concat(_cc).Concat(_bcc).ToList().AsReadOnly();

    public bool HasAttachments => _attachments.Count > 0;
}
