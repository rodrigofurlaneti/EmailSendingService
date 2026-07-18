using EmailSendingService.Domain.Entities;
using EmailSendingService.Domain.Exceptions;
using EmailSendingService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace EmailSendingService.UnitTests.Domain;

public class EmailMessageTests
{
    private static EmailAddress From => EmailAddress.Create("from@example.com");
    private static EmailAddress To => EmailAddress.Create("to@example.com");

    [Fact]
    public void Create_WithValidData_ProducesConsistentAggregate()
    {
        var message = EmailMessage.Create(From, new[] { To }, "Hi", "Body");

        message.Id.Should().NotBeEmpty();
        message.To.Should().ContainSingle();
        message.Subject.Should().Be("Hi");
        message.BodyFormat.Should().Be(EmailBodyFormat.PlainText);
        message.HasAttachments.Should().BeFalse();
    }

    [Fact]
    public void Create_WithNoRecipients_Throws()
    {
        var act = () => EmailMessage.Create(From, Array.Empty<EmailAddress>(), "Hi", "Body");
        act.Should().Throw<EmailMessageValidationException>().WithMessage("*at least one recipient*");
    }

    [Theory]
    [InlineData("", "body")]
    [InlineData("subject", "")]
    public void Create_WithMissingSubjectOrBody_Throws(string subject, string body)
    {
        var act = () => EmailMessage.Create(From, new[] { To }, subject, body);
        act.Should().Throw<EmailMessageValidationException>();
    }

    [Fact]
    public void AllRecipients_CombinesToCcBcc()
    {
        var cc = EmailAddress.Create("cc@example.com");
        var bcc = EmailAddress.Create("bcc@example.com");

        var message = EmailMessage.Create(From, new[] { To }, "s", "b",
            cc: new[] { cc }, bcc: new[] { bcc });

        message.AllRecipients().Select(r => r.Value)
            .Should().BeEquivalentTo("to@example.com", "cc@example.com", "bcc@example.com");
    }

    [Fact]
    public void Create_OnlyBccRecipient_IsAllowed()
    {
        var bcc = EmailAddress.Create("bcc@example.com");
        var act = () => EmailMessage.Create(From, Array.Empty<EmailAddress>(), "s", "b", bcc: new[] { bcc });
        act.Should().NotThrow();
    }
}
