using EmailSendingService.Domain.Entities;

namespace EmailSendingService.Application.Abstractions;

/// <summary>
/// Outbound port: the Application layer depends on this abstraction; the
/// Infrastructure layer provides the concrete SMTP implementation.
/// (Dependency Inversion Principle.)
/// </summary>
public interface IEmailSender
{
    Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
