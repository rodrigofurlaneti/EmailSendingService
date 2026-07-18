using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EmailSendingService.UnitTests.Infrastructure;

/// <summary>
/// A minimal loopback SMTP server used to prove that the real socket-based
/// transport (SmtpTransport + SmtpSession) speaks correct SMTP end to end.
/// </summary>
public sealed class InMemorySmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Task _acceptLoop;

    public int Port { get; }
    public List<string> ReceivedCommands { get; } = new();
    public string? MailFrom { get; private set; }
    public List<string> Recipients { get; } = new();
    public string ReceivedData { get; private set; } = string.Empty;

    public InMemorySmtpServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptOneAsync);
    }

    private async Task AcceptOneAsync()
    {
        using var client = await _listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

        await writer.WriteLineAsync("220 localhost ESMTP test");

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            ReceivedCommands.Add(line);

            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250-localhost");
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase))
            {
                MailFrom = ExtractAddress(line);
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
            {
                Recipients.Add(ExtractAddress(line));
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 Start mail input; end with <CRLF>.<CRLF>");
                var body = new StringBuilder();
                string? dataLine;
                while ((dataLine = await reader.ReadLineAsync()) is not null)
                {
                    if (dataLine == ".") break;
                    // Undo dot-stuffing.
                    if (dataLine.StartsWith('.')) dataLine = dataLine[1..];
                    body.AppendLine(dataLine);
                }
                ReceivedData = body.ToString();
                await writer.WriteLineAsync("250 OK: queued as ABC123");
            }
            else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 Bye");
                break;
            }
            else
            {
                await writer.WriteLineAsync("250 OK");
            }
        }
    }

    private static string ExtractAddress(string command)
    {
        var start = command.IndexOf('<');
        var end = command.IndexOf('>');
        return start >= 0 && end > start ? command[(start + 1)..end] : command;
    }

    public async ValueTask DisposeAsync()
    {
        try { await _acceptLoop.WaitAsync(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _listener.Stop();
    }
}
