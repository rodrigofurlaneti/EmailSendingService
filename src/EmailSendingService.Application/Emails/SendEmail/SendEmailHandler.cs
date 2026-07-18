using EmailSendingService.Application.Abstractions;
using EmailSendingService.Application.Common;
using EmailSendingService.Application.Emails.Dtos;
using EmailSendingService.Domain.Exceptions;

namespace EmailSendingService.Application.Emails.SendEmail;

/// <summary>
/// Orchestrates the "send e-mail" use case: map DTO -&gt; domain, then dispatch
/// through the outbound port. Contains no transport details.
/// </summary>
public sealed class SendEmailHandler
{
    private readonly IEmailSender _sender;
    private readonly IEmailDefaultsProvider _defaults;

    public SendEmailHandler(IEmailSender sender, IEmailDefaultsProvider defaults)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
    }

    public async Task<Result<SendEmailResponse>> HandleAsync(
        SendEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            var message = SendEmailMapper.ToDomain(
                command.Request,
                _defaults.DefaultFromAddress,
                _defaults.DefaultFromName);

            var delivery = await _sender.SendAsync(message, cancellationToken);

            var response = new SendEmailResponse
            {
                EmailId = message.Id,
                ProviderMessageId = delivery.ProviderMessageId,
                Delivered = delivery.Success,
                SentAtUtc = DateTimeOffset.UtcNow
            };

            return Result<SendEmailResponse>.Success(response);
        }
        catch (DomainException ex)
        {
            // Expected, caller-fixable validation problems.
            return Result<SendEmailResponse>.Failure(ex.Message);
        }
    }
}
