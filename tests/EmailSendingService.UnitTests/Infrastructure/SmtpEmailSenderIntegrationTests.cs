using EmailSendingService.Domain.Entities;
using EmailSendingService.Domain.ValueObjects;
using EmailSendingService.Infrastructure.Configuration;
using EmailSendingService.Infrastructure.Smtp;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EmailSendingService.UnitTests.Infrastructure;

/// <summary>
/// End-to-end test over a real TCP socket against a loopback SMTP server. Proves
/// the "100% C#" transport actually delivers a message via the SMTP protocol.
/// </summary>
public class SmtpEmailSenderIntegrationTests
{
    [Fact]
    public async Task SendAsync_DeliversMessageOverSocket()
    {
        await using var server = new InMemorySmtpServer();

        var settings = new SmtpSettings
        {
            Host = "127.0.0.1",
            Port = server.Port,
            UseStartTls = false,
            UseImplicitTls = false,
            ClientHostName = "test-client",
            TimeoutMilliseconds = 5000
        };

        var sender = new SmtpEmailSender(
            Options.Create(settings),
            NullLogger<SmtpEmailSender>.Instance);

        var message = EmailMessage.Create(
            EmailAddress.Create("from@example.com", "Sender"),
            new[] { EmailAddress.Create("to@example.com") },
            "Integration Subject",
            "Hello over sockets",
            cc: new[] { EmailAddress.Create("cc@example.com") });

        var result = await sender.SendAsync(message);

        result.Success.Should().BeTrue();
        server.MailFrom.Should().Be("from@example.com");
        server.Recipients.Should().BeEquivalentTo("to@example.com", "cc@example.com");
        server.ReceivedData.Should().Contain("Subject: Integration Subject");
        server.ReceivedCommands.Should().Contain("QUIT");
    }
}
