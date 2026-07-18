using EmailSendingService.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace EmailSendingService.Infrastructure.Configuration;

/// <summary>Exposes the configured default sender to the Application layer.</summary>
public sealed class SmtpEmailDefaultsProvider : IEmailDefaultsProvider
{
    private readonly SmtpSettings _settings;

    public SmtpEmailDefaultsProvider(IOptions<SmtpSettings> settings) => _settings = settings.Value;

    public string? DefaultFromAddress =>
        string.IsNullOrWhiteSpace(_settings.DefaultFromAddress) ? null : _settings.DefaultFromAddress;

    public string? DefaultFromName => _settings.DefaultFromName;
}
