using System.Security.Cryptography;
using System.Text;
using EmailSendingService.Infrastructure.Smtp;
using FluentAssertions;
using Xunit;

namespace EmailSendingService.UnitTests.Infrastructure;

public class DkimSignerTests
{
    private static readonly string Message =
        "From: \"Rodrigo\" <sender@mydomain.com>\r\n" +
        "To: dest@example.com\r\n" +
        "Subject: Teste DKIM\r\n" +
        "Date: Sat, 18 Jul 2026 15:00:00 GMT\r\n" +
        "Message-ID: <abc@mydomain.com>\r\n" +
        "MIME-Version: 1.0\r\n" +
        "Content-Type: text/plain; charset=utf-8\r\n" +
        "Content-Transfer-Encoding: base64\r\n" +
        "\r\n" +
        Convert2("corpo do email");

    private static string Convert2(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)) + "\r\n";

    [Fact]
    public void Sign_PrependsDkimSignatureHeader()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportPkcs8PrivateKeyPem();

        var signed = DkimSigner.Sign(Message, "mydomain.com", "default", pem);

        signed.Should().StartWith("DKIM-Signature: v=1; a=rsa-sha256; c=relaxed/relaxed;");
        signed.Should().Contain("d=mydomain.com");
        signed.Should().Contain("s=default");
        signed.Should().Contain("bh=");
        signed.Should().Contain("h=from:to:subject:date:message-id");
    }

    [Fact]
    public void Signature_VerifiesWithPublicKey()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportPkcs8PrivateKeyPem();

        var signed = DkimSigner.Sign(Message, "mydomain.com", "default", pem);

        // Extract the DKIM-Signature header value (first line, unfolded).
        var firstLine = signed[..signed.IndexOf("\r\n", StringComparison.Ordinal)];
        var value = firstLine["DKIM-Signature: ".Length..];

        var b = Tag(value, "b");
        var bh = Tag(value, "bh");

        // Recompute the body hash and compare.
        var (_, body) = DkimSigner.SplitMessage(Message);
        var expectedBh = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(DkimSigner.CanonicalizeBody(body))));
        bh.Should().Be(expectedBh);

        // Rebuild the signed data (headers + DKIM-Signature with empty b) and verify.
        var valueEmptyB = value[..(value.IndexOf("b=", StringComparison.Ordinal) + 2)];
        var (headerBlock, _) = DkimSigner.SplitMessage(Message);
        var headers = DkimSigner.ParseHeaders(headerBlock);
        var order = new[] { "from", "to", "subject", "date", "message-id" };

        var sb = new StringBuilder();
        foreach (var h in order)
            sb.Append(DkimSigner.CanonicalizeHeader(h, headers[h])).Append("\r\n");
        sb.Append(DkimSigner.CanonicalizeHeader("dkim-signature", valueEmptyB));

        var ok = rsa.VerifyData(
            Encoding.UTF8.GetBytes(sb.ToString()),
            Convert.FromBase64String(b),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        ok.Should().BeTrue("the DKIM signature must verify against the signing key's public key");
    }

    private static string Tag(string headerValue, string tag)
    {
        foreach (var part in headerValue.Split(';', StringSplitOptions.TrimEntries))
            if (part.StartsWith(tag + "=", StringComparison.Ordinal))
                return part[(tag.Length + 1)..];
        return string.Empty;
    }
}
