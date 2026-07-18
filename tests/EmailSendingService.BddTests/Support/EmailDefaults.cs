using EmailSendingService.Application.Abstractions;

namespace EmailSendingService.BddTests.Support;

public sealed class EmailDefaults : IEmailDefaultsProvider
{
    public string? DefaultFromAddress => "no-reply@example.com";
    public string? DefaultFromName => "Test";
}
