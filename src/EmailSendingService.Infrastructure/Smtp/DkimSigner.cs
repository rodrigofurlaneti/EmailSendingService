using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EmailSendingService.Infrastructure.Configuration;

namespace EmailSendingService.Infrastructure.Smtp;

/// <summary>
/// Signs an RFC 5322 message with DKIM (RFC 6376), 100% in C# using the BCL's
/// RSA + SHA-256. Uses relaxed/relaxed canonicalization. The resulting
/// "DKIM-Signature:" header is prepended to the message so receiving servers
/// can verify it against the public key published in DNS.
/// </summary>
public static partial class DkimSigner
{
    private static readonly string[] DefaultHeaders = { "from", "to", "cc", "subject", "date", "message-id" };

    [GeneratedRegex("[ \t]+")]
    private static partial Regex WspRegex();

    /// <summary>Signs the message when DKIM is enabled; otherwise returns it unchanged.</summary>
    public static string ApplyIfEnabled(string message, DkimOptions? options)
    {
        if (options is null || !options.Enabled)
            return message;

        var pem = options.PrivateKeyPem;
        if (string.IsNullOrWhiteSpace(pem) &&
            !string.IsNullOrWhiteSpace(options.PrivateKeyPath) &&
            File.Exists(options.PrivateKeyPath))
        {
            pem = File.ReadAllText(options.PrivateKeyPath);
        }

        return Sign(message, options.Domain, options.Selector, pem);
    }

    /// <summary>Returns the message with a DKIM-Signature header prepended.</summary>
    public static string Sign(string message, string domain, string selector, string privateKeyPem, IEnumerable<string>? headersToSign = null)
    {
        if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("DKIM domain is required.", nameof(domain));
        if (string.IsNullOrWhiteSpace(selector)) throw new ArgumentException("DKIM selector is required.", nameof(selector));
        if (string.IsNullOrWhiteSpace(privateKeyPem)) throw new ArgumentException("DKIM private key is required.", nameof(privateKeyPem));

        var (headerBlock, body) = SplitMessage(message);
        var headers = ParseHeaders(headerBlock);

        // 1) Body hash over the relaxed-canonicalized body.
        var bodyHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalizeBody(body))));

        // 2) Only sign headers that are actually present, in the requested order.
        var wanted = (headersToSign ?? DefaultHeaders).Select(h => h.ToLowerInvariant());
        var signedHeaders = wanted.Where(headers.ContainsKey).ToList();
        var hTag = string.Join(":", signedHeaders);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 3) DKIM-Signature header value with an empty b= (used while computing the signature).
        var dkimValueUnsigned =
            $"v=1; a=rsa-sha256; c=relaxed/relaxed; d={domain}; s={selector}; " +
            $"t={timestamp}; bh={bodyHash}; h={hTag}; b=";

        // 4) Build the string to sign: canonicalized signed headers + the DKIM-Signature
        //    header itself (with empty b=), the latter WITHOUT a trailing CRLF.
        var sb = new StringBuilder();
        foreach (var name in signedHeaders)
            sb.Append(CanonicalizeHeader(name, headers[name])).Append("\r\n");
        sb.Append(CanonicalizeHeader("dkim-signature", dkimValueUnsigned));

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(sb.ToString()), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var b = Convert.ToBase64String(signature);

        var finalHeader = "DKIM-Signature: " + dkimValueUnsigned + b;
        return finalHeader + "\r\n" + message;
    }

    // ---- canonicalization (internal for unit testing) ----

    internal static (string headers, string body) SplitMessage(string message)
    {
        var idx = message.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (idx < 0)
            return (message, string.Empty);
        return (message[..idx], message[(idx + 4)..]);
    }

    /// <summary>Parses header lines into a case-insensitive map, unfolding continuations.</summary>
    internal static Dictionary<string, string> ParseHeaders(string headerBlock)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentName = null;
        var currentValue = new StringBuilder();

        void Flush()
        {
            if (currentName is not null)
                result[currentName] = currentValue.ToString();
            currentName = null;
            currentValue.Clear();
        }

        foreach (var line in headerBlock.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
            {
                currentValue.Append(' ').Append(line.Trim());
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0) continue;

            Flush();
            currentName = line[..colon].Trim();
            currentValue.Append(line[(colon + 1)..].Trim());
        }
        Flush();
        return result;
    }

    /// <summary>Relaxed header canonicalization (RFC 6376 §3.4.2).</summary>
    internal static string CanonicalizeHeader(string name, string value)
    {
        var unfolded = value.Replace("\r\n", " ");
        var collapsed = WspRegex().Replace(unfolded, " ").Trim();
        return name.ToLowerInvariant() + ":" + collapsed;
    }

    /// <summary>Relaxed body canonicalization (RFC 6376 §3.4.4).</summary>
    internal static string CanonicalizeBody(string body)
    {
        var normalized = body.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n')
            .Select(l => WspRegex().Replace(l, " ").TrimEnd(' '));

        var text = string.Join("\r\n", lines);

        // Remove all trailing empty lines, then ensure a single terminating CRLF.
        text = text.TrimEnd('\r', '\n');
        return text.Length == 0 ? string.Empty : text + "\r\n";
    }
}
