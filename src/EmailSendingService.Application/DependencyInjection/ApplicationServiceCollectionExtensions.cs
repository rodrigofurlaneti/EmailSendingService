using EmailSendingService.Application.Emails.SendEmail;
using Microsoft.Extensions.DependencyInjection;

namespace EmailSendingService.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SendEmailHandler>();
        return services;
    }
}
