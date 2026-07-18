using System.Text;
using EmailSendingService.Domain.Entities;
using EmailSendingService.Domain.ValueObjects;
using EmailSendingService.Infrastructure.Smtp;
using FluentAssertions;
using Xunit;

namespace EmailSendingService.UnitTests.Infrastructure;

public class MimeMessageBuilderTests
{
    private static EmailAddress From => EmailAddress.Create("from@example.com", "Sender");
    private static EmailAddress To => EmailAddress.Create("to@example.com");

    [Fact]
    public void Build_PlainText_ContainsRequiredHeaders()
    {
        var message = EmailMessage.Create(From, new[] { To }, "Hello", "Plain body");
        var mime = MimeMessageBuilder.Build(message);

        mime.Should().Contain("From: \"Sender\" <from@example.com>");
        mime.Should().Contain("To: to@example.com");
        mime.Should().Contain("Subject: Hello");
        mime.Should().Contain("MIME-Version: 1.0");
        mime.Should().Contain("Content-Type: text/plain; charset=utf-8");
        mime.Should().Contain("Message-ID: <");
    }

    [Fact]
    public void Build_DoesNotLeakBccIntoHeaders()
    {
        var bcc = EmailAddress.Create("secret@example.com");
        var message = EmailMessage.Create(From, new[] { To }, "s", "b", bcc: new[] { bcc });

        MimeMessageBuilder.Build(message).Should().NotContain("secret@example.com");
    }

    [Fact]
    public void Build_Html_SetsHtmlContentType()
    {
        var message = EmailMessage.Create(From, new[] { To }, "s", "<b>hi</b>", EmailBodyFormat.Html);
        MimeMessageBuilder.Build(message).Should().Contain("Content-Type: text/html; charset=utf-8");
    }

    [Fact]
    public void Build_WithAttachment_ProducesMultipartMixed()
    {
        var attachment = EmailAttachment.Create("hello.txt", "text/plain", Encoding.UTF8.GetBytes("file-content"));
        var message = EmailMessage.Create(From, new[] { To }, "s", "b", attachments: new[] { attachment });

        var mime = MimeMessageBuilder.Build(message);
        mime.Should().Contain("Content-Type: multipart/mixed; boundary=");
        mime.Should().Contain("Content-Disposition: attachment; filename=\"hello.txt\"");
        mime.Should().Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("file-content")));
    }

    [Fact]
    public void EncodeHeaderValue_NonAscii_UsesEncodedWord()
    {
        var encoded = MimeMessageBuilder.EncodeHeaderValue("Olá Café");
        encoded.Should().StartWith("=?UTF-8?B?").And.EndWith("?=");
    }

    [Fact]
    public void ToBase64Lines_WrapsAt76Characters()
    {
        var data = new byte[300];
        var lines = MimeMessageBuilder.ToBase64Lines(data).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines.Should().OnlyContain(l => l.Length <= 76);
    }
}
