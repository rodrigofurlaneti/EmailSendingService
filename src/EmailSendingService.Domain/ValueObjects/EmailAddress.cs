using System.Text.RegularExpressions;
using EmailSendingService.Domain.Common;
using EmailSendingService.Domain.Exceptions;

namespace EmailSendingService.Domain.ValueObjects;

/// <summary>
/// A validated e-mail address. Guarantees, by construction, that no invalid
/// address can ever exist inside the domain model.
/// </summary>
public sealed partial class EmailAddress : ValueObject
{
    // Pragmatic, RFC 5322 inspired validation (not the full grammar, which is
    // impractical and rarely desirable in real systems).
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AddressRegex();

    public string Value { get; }

    /// <summary>Optional display name, e.g. "John Doe" &lt;john@doe.com&gt;.</summary>
    public string? DisplayName { get; }

    private EmailAddress(string value, string? displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public static EmailAddress Create(string value, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidEmailAddressException(value ?? "<null>");

        var normalized = value.Trim();
        if (!AddressRegex().IsMatch(normalized))
            throw new InvalidEmailAddressException(value);

        var name = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        return new EmailAddress(normalized, name);
    }

    /// <summary>The domain part of the address (after the '@').</summary>
    public string Domain => Value[(Value.IndexOf('@') + 1)..];

    /// <summary>Renders the address for an RFC 5322 header ("Name" &lt;addr&gt;).</summary>
    public string ToRfcString()
        => DisplayName is null ? Value : $"\"{DisplayName}\" <{Value}>";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
        yield return DisplayName;
    }

    public override string ToString() => ToRfcString();
}
