using EmailSendingService.Application.Abstractions;
using EmailSendingService.Domain.Entities;
using EmailSendingService.Infrastructure.Configuration;
using EmailSendingService.Infrastructure.Dns;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmailSendingService.Infrastructure.Smtp;

/// <summary>
/// Delivers mail as a Mail Transfer Agent (MTA): the message is not relayed
/// through any external SMTP server. Recipients are grouped by domain, the MX
/// hosts are resolved via DNS, and the service connects directly to each MX on
/// port 25 and runs the SMTP exchange itself — 100% C#, self-contained.
/// </summary>
public sealed class DirectSmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly IMxResolver _mxResolver;
    private readonly ILogger<DirectSmtpEmailSender> _logger;
    private readonly Func<SmtpSettings, SmtpTransport> _transportFactory;

    public DirectSmtpEmailSender(
        IOptions<SmtpSettings> settings,
        IMxResolver mxResolver,
        ILogger<DirectSmtpEmailSender> logger,
        Func<SmtpSettings, SmtpTransport>? transportFactory = null)
    {
        _settings = settings.Value;
        _mxResolver = mxResolver;
        _logger = logger;
        _transportFactory = transportFactory ?? (s => new SmtpTransport(s));
    }

    public async Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = MimeMessageBuilder.Build(message);
        var replies = new List<string>();

        var byDomain = message.AllRecipients()
            .GroupBy(r => r.Domain, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byDomain)
        {
            var domain = group.Key;
            var recipients = group.Select(r => r.Value).ToList();

            var mxHosts = await _mxResolver.ResolveAsync(domain, cancellationToken);
            if (mxHosts.Count == 0)
                throw new SmtpException($"No MX host found for domain '{domain}'.");

            Exception? lastError = null;
            var deliveredToDomain = false;

            foreach (var mx in mxHosts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var target = BuildTargetSettings(mx);
                    await using var transport = _transportFactory(target);

                    var handshake = await transport.OpenAsync(cancellationToken);
                    replies.AddRange(handshake);

                    var session = transport.Session;
                    await session.MailFromAsync(message.From.Value, cancellationToken);

                    foreach (var recipient in recipients)
                        await session.RcptToAsync(recipient, cancellationToken);

                    var dataReply = await session.DataAsync(mime, cancellationToken);
                    replies.Add($"[{domain} via {mx}] {dataReply}");

                    await session.QuitAsync(cancellationToken);

                    _logger.LogInformation(
                        "Delivered e-mail {EmailId} to domain {Domain} via MX {Mx} ({Count} recipient(s)).",
                        message.Id, domain, mx, recipients.Count);

                    deliveredToDomain = true;
                    break; // this domain is done; do not try lower-priority MX hosts
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _logger.LogWarning(ex, "MX {Mx} for domain {Domain} failed; trying the next one.", mx, domain);
                }
            }

            if (!deliveredToDomain)
                throw new SmtpException(
                    $"Delivery to domain '{domain}' failed on all {mxHosts.Count} MX host(s).",
                    inner: lastError);
        }

        var providerMessageId = $"{message.Id:N}@{message.From.Domain}";
        return EmailDeliveryResult.Ok(providerMessageId, replies);
    }

    private SmtpSettings BuildTargetSettings(string mxHost) => new()
    {
        Host = mxHost,
        Port = _settings.DirectMxPort,
        // Opportunistic TLS: many public MX servers offer STARTTLS with certificates
        // that will not chain to our trust store, so we accept them (standard for MTAs).
        UseStartTls = true,
        UseImplicitTls = false,
        AllowInvalidCertificates = true,
        Username = null,
        Password = null,
        ClientHostName = _settings.ClientHostName,
        TimeoutMilliseconds = _settings.TimeoutMilliseconds
    };
}
