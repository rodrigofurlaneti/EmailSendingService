using System.Globalization;
using System.Text;
using EmailSendingService.Domain.Entities;
using EmailSendingService.Domain.ValueObjects;

namespace EmailSendingService.Infrastructure.Smtp;

/// <summary>
/// Serialises an <see cref="EmailMessage"/> into an RFC 5322 / MIME text blob,
/// entirely in C# — no external mail library. Supports UTF-8 headers/bodies via
/// encoded-words and Base64, and multipart/mixed when attachments are present.
/// </summary>
public static class MimeMessageBuilder
{
    public static string Build(EmailMessage message)
    {
        var sb = new StringBuilder();
        var boundary = "==_Boundary_" + Guid.NewGuid().ToString("N");

        sb.Append("From: ").Append(FormatAddress(message.From)).Append("\r\n");

        if (message.To.Count > 0)
            sb.Append("To: ").Append(FormatAddressList(message.To)).Append("\r\n");
        if (message.Cc.Count > 0)
            sb.Append("Cc: ").Append(FormatAddressList(message.Cc)).Append("\r\n");
        // Bcc recipients are delivered via RCPT TO but never written into headers.

        sb.Append("Subject: ").Append(EncodeHeaderValue(message.Subject)).Append("\r\n");
        sb.Append("Date: ").Append(message.CreatedAt.ToString("r", CultureInfo.InvariantCulture)).Append("\r\n");
        sb.Append("Message-ID: <").Append(message.Id.ToString("N")).Append('@').Append(message.From.Domain).Append(">\r\n");
        sb.Append("MIME-Version: 1.0\r\n");

        var contentType = message.BodyFormat == EmailBodyFormat.Html ? "text/html" : "text/plain";

        if (!message.HasAttachments)
        {
            sb.Append("Content-Type: ").Append(contentType).Append("; charset=utf-8\r\n");
            sb.Append("Content-Transfer-Encoding: base64\r\n");
            sb.Append("\r\n");
            sb.Append(ToBase64Lines(Encoding.UTF8.GetBytes(message.Body)));
            return sb.ToString();
        }

        // multipart/mixed: one body part + N attachment parts.
        sb.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append("\"\r\n");
        sb.Append("\r\n");
        sb.Append("This is a multi-part message in MIME format.\r\n");

        sb.Append("--").Append(boundary).Append("\r\n");
        sb.Append("Content-Type: ").Append(contentType).Append("; charset=utf-8\r\n");
        sb.Append("Content-Transfer-Encoding: base64\r\n\r\n");
        sb.Append(ToBase64Lines(Encoding.UTF8.GetBytes(message.Body)));
        sb.Append("\r\n");

        foreach (var attachment in message.Attachments)
        {
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("Content-Type: ").Append(attachment.ContentType).Append("; name=\"")
              .Append(EncodeHeaderValue(attachment.FileName)).Append("\"\r\n");
            sb.Append("Content-Transfer-Encoding: base64\r\n");
            sb.Append("Content-Disposition: attachment; filename=\"")
              .Append(EncodeHeaderValue(attachment.FileName)).Append("\"\r\n\r\n");
            sb.Append(ToBase64Lines(attachment.Content));
            sb.Append("\r\n");
        }

        sb.Append("--").Append(boundary).Append("--\r\n");
        return sb.ToString();
    }

    private static string FormatAddressList(IEnumerable<EmailAddress> addresses)
        => string.Join(", ", addresses.Select(FormatAddress));

    private static string FormatAddress(EmailAddress address)
    {
        if (address.DisplayName is null)
            return address.Value;

        // ASCII names are quoted (safe for commas/special chars); non-ASCII names
        // use an RFC 2047 encoded-word, which must NOT be wrapped in quotes.
        var rendered = IsAscii(address.DisplayName)
            ? $"\"{address.DisplayName}\""
            : EncodeHeaderValue(address.DisplayName);

        return $"{rendered} <{address.Value}>";
    }

    /// <summary>RFC 2047 encoded-word for non-ASCII header values; plain otherwise.</summary>
    internal static string EncodeHeaderValue(string value)
    {
        if (IsAscii(value))
            return value;

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return $"=?UTF-8?B?{encoded}?=";
    }

    private static bool IsAscii(string value)
    {
        foreach (var c in value)
            if (c > 127) return false;
        return true;
    }

    /// <summary>Base64 wrapped at 76 characters per line as required by MIME.</summary>
    internal static string ToBase64Lines(byte[] data)
    {
        var base64 = Convert.ToBase64String(data);
        var sb = new StringBuilder(base64.Length + base64.Length / 76 * 2);
        for (int i = 0; i < base64.Length; i += 76)
        {
            var len = Math.Min(76, base64.Length - i);
            sb.Append(base64, i, len).Append("\r\n");
        }
        return sb.ToString();
    }
}
