using System.Text;

namespace EmailSendingService.Infrastructure.Smtp;

/// <summary>
/// Implements the RFC 5321 SMTP command/response conversation over an arbitrary
/// duplex text channel. It knows nothing about sockets or TLS, which makes the
/// protocol logic fully unit-testable with in-memory streams.
/// </summary>
public sealed class SmtpSession
{
    private readonly TextReader _reader;
    private readonly TextWriter _writer;

    public SmtpSession(TextReader reader, TextWriter writer)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>Reads a (possibly multi-line) reply. Lines look like "250-KEY" ... "250 DONE".</summary>
    public async Task<SmtpReply> ReadReplyAsync(CancellationToken ct = default)
    {
        var lines = new List<string>();
        int code = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await _reader.ReadLineAsync(ct);
            if (line is null)
                throw new SmtpException("Connection closed by server while awaiting a reply.");

            if (line.Length < 3 || !int.TryParse(line.AsSpan(0, 3), out code))
                throw new SmtpException($"Malformed SMTP reply: '{line}'.");

            // A hyphen at position 3 means "more lines follow".
            var isLast = line.Length == 3 || line[3] != '-';
            lines.Add(line.Length > 4 ? line[4..] : string.Empty);

            if (isLast)
                break;
        }

        return new SmtpReply(code, lines);
    }

    public async Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        await _writer.WriteAsync(line.AsMemory(), ct);
        await _writer.WriteAsync("\r\n".AsMemory(), ct);
        await _writer.FlushAsync(ct);
    }

    /// <summary>Sends a command and returns the reply, asserting an expected code range.</summary>
    public async Task<SmtpReply> SendCommandAsync(
        string command,
        Func<SmtpReply, bool> isExpected,
        CancellationToken ct = default)
    {
        await WriteLineAsync(command, ct);
        var reply = await ReadReplyAsync(ct);
        if (!isExpected(reply))
            throw new SmtpException(
                $"Unexpected reply to '{Sanitize(command)}': {reply}", reply.Code);
        return reply;
    }

    public Task<SmtpReply> EhloAsync(string clientHost, CancellationToken ct = default)
        => SendCommandAsync($"EHLO {clientHost}", r => r.IsPositiveCompletion, ct);

    public Task<SmtpReply> HeloAsync(string clientHost, CancellationToken ct = default)
        => SendCommandAsync($"HELO {clientHost}", r => r.IsPositiveCompletion, ct);

    public async Task AuthLoginAsync(string username, string password, CancellationToken ct = default)
    {
        await SendCommandAsync("AUTH LOGIN", r => r.Code == 334, ct);
        await SendCommandAsync(Base64(username), r => r.Code == 334, ct);
        await SendCommandAsync(Base64(password), r => r.Code == 235, ct);
    }

    public Task MailFromAsync(string fromAddress, CancellationToken ct = default)
        => SendCommandAsync($"MAIL FROM:<{fromAddress}>", r => r.IsPositiveCompletion, ct);

    public Task RcptToAsync(string recipient, CancellationToken ct = default)
        => SendCommandAsync($"RCPT TO:<{recipient}>", r => r.IsPositiveCompletion, ct);

    /// <summary>Runs DATA: sends the raw message (with dot-stuffing) and terminates it.</summary>
    public async Task<SmtpReply> DataAsync(string rawMessage, CancellationToken ct = default)
    {
        await SendCommandAsync("DATA", r => r.Code == 354, ct);

        var payload = DotStuff(rawMessage);
        await _writer.WriteAsync(payload.AsMemory(), ct);
        await _writer.WriteAsync("\r\n.\r\n".AsMemory(), ct);
        await _writer.FlushAsync(ct);

        var reply = await ReadReplyAsync(ct);
        if (!reply.IsPositiveCompletion)
            throw new SmtpException($"Server rejected message body: {reply}", reply.Code);
        return reply;
    }

    public async Task QuitAsync(CancellationToken ct = default)
    {
        try
        {
            await WriteLineAsync("QUIT", ct);
            await ReadReplyAsync(ct);
        }
        catch
        {
            // QUIT failures are non-fatal — the message is already accepted.
        }
    }

    /// <summary>RFC 5321 transparency: a line starting with '.' gets an extra leading '.'.</summary>
    internal static string DotStuff(string message)
    {
        var normalized = message.Replace("\r\n", "\n").Replace("\r", "\n");
        var sb = new StringBuilder(normalized.Length + 16);
        var lines = normalized.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith('.'))
                sb.Append('.');
            sb.Append(line);
            if (i < lines.Length - 1)
                sb.Append("\r\n");
        }
        return sb.ToString();
    }

    private static string Base64(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string Sanitize(string command)
        => command.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase) ? "AUTH ..." : command;
}
