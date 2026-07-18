using EmailSendingService.Domain.Exceptions;
using EmailSendingService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace EmailSendingService.UnitTests.Domain;

public class EmailAddressTests
{
    [Theory]
    [InlineData("john@doe.com")]
    [InlineData("a.b+tag@sub.domain.co")]
    public void Create_WithValidAddress_Succeeds(string value)
    {
        var address = EmailAddress.Create(value);
        address.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("no@domain")]
    [InlineData("two@@at.com")]
    public void Create_WithInvalidAddress_Throws(string value)
    {
        var act = () => EmailAddress.Create(value);
        act.Should().Throw<InvalidEmailAddressException>();
    }

    [Fact]
    public void Create_TrimsWhitespace()
        => EmailAddress.Create("  john@doe.com  ").Value.Should().Be("john@doe.com");

    [Fact]
    public void Domain_ReturnsPartAfterAt()
        => EmailAddress.Create("john@doe.com").Domain.Should().Be("doe.com");

    [Fact]
    public void ToRfcString_WithDisplayName_FormatsAsNameAndAngleBrackets()
        => EmailAddress.Create("john@doe.com", "John Doe")
            .ToRfcString().Should().Be("\"John Doe\" <john@doe.com>");

    [Fact]
    public void Equality_IsCaseInsensitiveOnAddress()
        => EmailAddress.Create("John@Doe.com").Should().Be(EmailAddress.Create("john@doe.com"));
}
