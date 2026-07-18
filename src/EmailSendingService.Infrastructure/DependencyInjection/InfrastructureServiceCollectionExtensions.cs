using EmailSendingService.Application.Abstractions;
using EmailSendingService.Infrastructure.Configuration;
using EmailSendingService.Infrastructure.Dns;
using EmailSendingService.Infrastructure.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EmailSendingService.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpSettings>()
            .Bind(configuration.GetSection(SmtpSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IEmailDefaultsProvider, SmtpEmailDefaultsProvider>();

        // DNS-based MX resolver used by the direct (MTA) sender.
        services.AddSingleton<IMxResolver>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<SmtpSettings>>().Value;
            return new DnsMxResolver(settings.DnsServer, settings.TimeoutMilliseconds);
        });

        // Pick the delivery strategy from configuration (default: DirectMx).
        var mode = configuration.GetSection(SmtpSettings.SectionName)
            .GetValue<EmailDeliveryMode>(nameof(SmtpSettings.DeliveryMode));

        if (mode == EmailDeliveryMode.Relay)
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        else
            services.AddScoped<IEmailSender, DirectSmtpEmailSender>();

        return services;
    }
}
