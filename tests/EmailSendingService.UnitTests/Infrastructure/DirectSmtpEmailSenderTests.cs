using EmailSendingService.Domain.Entities;
using EmailSendingService.Domain.ValueObjects;
using EmailSendingService.Infrastructure.Configuration;
using EmailSendingService.Infrastructure.Dns;
using EmailSendingService.Infrastructure.Smtp;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmailSendingService.UnitTests.Infrastructure;

public class DirectSmtpEmailSenderTests
{
    private sealed class StubMxResolver : IMxResolver
    {
        private readonly string _host;
        public List<string> ResolvedDomains { get; } = new();
        public StubMxResolver(string host) => _host = host;

        public Task<IReadOnlyList<string>> ResolveAsync(string domain, CancellationToken ct = default)
        {
            ResolvedDomains.Add(domain);
            return Task.FromResult<IReadOnlyList<string>>(new[] { _host });
        }
    }

    [Fact]
    public async Task SendAsync_ResolvesMxAndDeliversDirectlyOverSocket()
    {
        await using var server = new InMemorySmtpServer();

        var settings = new SmtpSettings
        {
            DeliveryMode = EmailDeliveryMode.DirectMx,
            DirectMxPort = server.Port,
            ClientHostName = "test-mta",
            TimeoutMilliseconds = 5000
        };
        var resolver = new StubMxResolver("127.0.0.1");

        var sender = new DirectSmtpEmailSender(
            Options.Create(settings),
            resolver,
            NullLogger<DirectSmtpEmailSender>.Instance);

        var message = EmailMessage.Create(
            EmailAddress.Create("sender@mycompany.com", "Me"),
            new[]
            {
                EmailAddress.Create("alice@example.com"),
                EmailAddress.Create("bob@example.com")
            },
            "Direct MX subject",
            "Delivered without any relay");

        var result = await sender.SendAsync(message);

        result.Success.Should().BeTrue();
        resolver.ResolvedDomains.Should().ContainSingle().Which.Should().Be("example.com");
        server.MailFrom.Should().Be("sender@mycompany.com");
        server.Recipients.Should().BeEquivalentTo("alice@example.com", "bob@example.com");
        server.ReceivedData.Should().Contain("Subject: Direct MX subject");
    }
}
