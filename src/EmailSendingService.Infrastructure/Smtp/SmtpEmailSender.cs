using EmailSendingService.Application.Abstractions;
using EmailSendingService.Domain.Entities;
using EmailSendingService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailSendingService.Infrastructure.Smtp;

/// <summary>
/// Concrete SMTP implementation of the <see cref="IEmailSender"/> port. Builds
/// the MIME payload, opens a raw SMTP transport and runs the full
/// MAIL FROM / RCPT TO / DATA exchange — 100% C#, no mail library.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly Func<SmtpSettings, SmtpTransport> _transportFactory;

    public SmtpEmailSender(
        IOptions<SmtpSettings> settings,
        ILogger<SmtpEmailSender> logger,
        Func<SmtpSettings, SmtpTransport>? transportFactory = null)
    {
        _settings = settings.Value;
        _logger = logger;
        _transportFactory = transportFactory ?? (s => new SmtpTransport(s));
    }

    public async Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var replies = new List<string>();
        var mime = MimeMessageBuilder.Build(message);

        await using var transport = _transportFactory(_settings);

        var handshake = await transport.OpenAsync(cancellationToken);
        replies.AddRange(handshake);

        var session = transport.Session;

        await session.MailFromAsync(message.From.Value, cancellationToken);

        foreach (var recipient in message.AllRecipients())
            await session.RcptToAsync(recipient.Value, cancellationToken);

        var dataReply = await session.DataAsync(mime, cancellationToken);
        replies.Add(dataReply.ToString());

        await session.QuitAsync(cancellationToken);

        var providerMessageId = $"{message.Id:N}@{message.From.Domain}";
        _logger.LogInformation(
            "E-mail {EmailId} accepted by {Host}:{Port} for {RecipientCount} recipient(s).",
            message.Id, _settings.Host, _settings.Port, message.AllRecipients().Count);

        return EmailDeliveryResult.Ok(providerMessageId, replies);
    }
}
