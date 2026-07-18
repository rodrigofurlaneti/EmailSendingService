using EmailSendingService.Application.Abstractions;
using EmailSendingService.Domain.Entities;

namespace EmailSendingService.BddTests.Support;

/// <summary>Test double that records dispatched messages instead of using real SMTP.</summary>
public sealed class InMemoryEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = new();

    public Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        var id = $"{message.Id:N}@{message.From.Domain}";
        return Task.FromResult(EmailDeliveryResult.Ok(id, new[] { "250 OK" }));
    }
}
