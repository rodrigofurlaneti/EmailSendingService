using EmailSendingService.Application.Abstractions;
using EmailSendingService.Application.Emails.Dtos;
using EmailSendingService.Application.Emails.SendEmail;
using EmailSendingService.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EmailSendingService.UnitTests.Application;

public class SendEmailHandlerTests
{
    private readonly IEmailSender _sender = Substitute.For<IEmailSender>();
    private readonly IEmailDefaultsProvider _defaults = Substitute.For<IEmailDefaultsProvider>();

    private SendEmailHandler CreateHandler() => new(_sender, _defaults);

    private static SendEmailRequest ValidRequest() => new()
    {
        From = new RecipientDto { Address = "from@example.com", Name = "Sender" },
        To = { new RecipientDto { Address = "to@example.com" } },
        Subject = "Hello",
        Body = "Body"
    };

    [Fact]
    public async Task HandleAsync_WithValidRequest_DispatchesAndReturnsSuccess()
    {
        _sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailDeliveryResult.Ok("msg-1", new[] { "250 OK" }));

        var result = await CreateHandler().HandleAsync(new SendEmailCommand(ValidRequest()));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Delivered.Should().BeTrue();
        result.Value.ProviderMessageId.Should().Be("msg-1");
        await _sender.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithInvalidEmail_ReturnsFailureAndDoesNotSend()
    {
        var request = ValidRequest();
        request.To[0].Address = "not-an-email";

        var result = await CreateHandler().HandleAsync(new SendEmailCommand(request));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        await _sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_IgnoresBlankAttachmentSlot_AndSends()
    {
        // Reproduces a payload with an empty "attachments":[{}] placeholder plus
        // the same address in To, Cc and Bcc.
        _sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailDeliveryResult.Ok("ok", Array.Empty<string>()));

        var request = new SendEmailRequest
        {
            From = new RecipientDto { Address = "rodrigofurlaneti31@hotmail.com", Name = "Rodrigo" },
            To = { new RecipientDto { Address = "rodrigofurlaneti31@hotmail.com", Name = "Rodrigo" } },
            Cc = { new RecipientDto { Address = "rodrigofurlaneti31@hotmail.com", Name = "Rodrigo" } },
            Bcc = { new RecipientDto { Address = "rodrigofurlaneti31@hotmail.com", Name = "Rodrigo" } },
            Subject = "teste assunto",
            Body = "teste body",
            IsHtml = false,
            Attachments = { new AttachmentDto { FileName = "", ContentType = "", ContentBase64 = "" } }
        };

        EmailMessage? captured = null;
        _sender.SendAsync(Arg.Do<EmailMessage>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(EmailDeliveryResult.Ok("ok", Array.Empty<string>()));

        var result = await CreateHandler().HandleAsync(new SendEmailCommand(request));

        result.IsSuccess.Should().BeTrue();
        captured!.HasAttachments.Should().BeFalse();
        captured.AllRecipients().Should().HaveCount(3);
    }

    [Fact]
    public async Task HandleAsync_UsesDefaultSender_WhenFromMissing()
    {
        _defaults.DefaultFromAddress.Returns("default@example.com");
        _defaults.DefaultFromName.Returns("Default");

        EmailMessage? captured = null;
        _sender.SendAsync(Arg.Do<EmailMessage>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(EmailDeliveryResult.Ok("x", Array.Empty<string>()));

        var request = ValidRequest();
        request.From = null;

        var result = await CreateHandler().HandleAsync(new SendEmailCommand(request));

        result.IsSuccess.Should().BeTrue();
        captured!.From.Value.Should().Be("default@example.com");
    }
}
