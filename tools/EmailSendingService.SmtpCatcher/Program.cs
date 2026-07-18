using System.Net;
using System.Net.Sockets;
using System.Text;

// A tiny, self-contained SMTP "catcher" (100% C#, no Docker, no external tools).
// It accepts messages on a local port, prints a summary and saves each one as an
// .eml file so you can confirm the API builds and dispatches e-mails correctly.
//
// Usage:
//   dotnet run --project tools/EmailSendingService.SmtpCatcher [port]
// Default port is 1025 (matches appsettings.Development.json in Relay mode).

int port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 1025;

var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "received-emails");
Directory.CreateDirectory(outputDir);

// Listen on both IPv4 and IPv6 so it works whether the client connects to
// 127.0.0.1 or ::1 (on Windows, "localhost" often resolves to IPv6 first).
var listener = new TcpListener(IPAddress.IPv6Any, port);
listener.Server.DualMode = true;
listener.Start();

Console.WriteLine("======================================================");
Console.WriteLine($"  SMTP Catcher ouvindo em localhost:{port}");
Console.WriteLine($"  E-mails serao salvos em: {outputDir}");
Console.WriteLine("  Pressione Ctrl+C para parar.");
Console.WriteLine("======================================================");

while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    _ = Task.Run(() => HandleClientAsync(client, outputDir));
}

static async Task HandleClientAsync(TcpClient client, string outputDir)
{
    try
    {
        using var _ = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };

        await writer.WriteLineAsync("220 smtp-catcher ESMTP ready");

        string from = "";
        var recipients = new List<string>();
        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250-smtp-catcher");
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase))
            {
                from = ExtractAddress(line);
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
            {
                recipients.Add(ExtractAddress(line));
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");

                var raw = new StringBuilder();
                string? dataLine;
                while ((dataLine = await reader.ReadLineAsync()) is not null)
                {
                    if (dataLine == ".") break;
                    if (dataLine.StartsWith('.')) dataLine = dataLine[1..]; // undo dot-stuffing
                    raw.AppendLine(dataLine);
                }

                var message = raw.ToString();
                await writer.WriteLineAsync("250 OK: message queued");
                SaveAndPrint(from, recipients, message, outputDir);
            }
            else if (line.StartsWith("RSET", StringComparison.OrdinalIgnoreCase))
            {
                from = "";
                recipients.Clear();
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("NOOP", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250 OK");
            }
            else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
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
    catch (Exception ex)
    {
        Console.WriteLine($"[erro] {ex.Message}");
    }
}

static void SaveAndPrint(string from, List<string> recipients, string message, string outputDir)
{
    var subject = ReadHeader(message, "Subject");
    var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.eml";
    var path = Path.Combine(outputDir, fileName);
    File.WriteAllText(path, message, new UTF8Encoding(false));

    Console.WriteLine();
    Console.WriteLine("------------------ E-MAIL RECEBIDO -------------------");
    Console.WriteLine($"  De        : {from}");
    Console.WriteLine($"  Para      : {string.Join(", ", recipients)}");
    Console.WriteLine($"  Assunto   : {subject}");
    Console.WriteLine($"  Salvo em  : {path}");
    Console.WriteLine("------------------------------------------------------");
}

static string ExtractAddress(string command)
{
    int start = command.IndexOf('<');
    int end = command.IndexOf('>');
    return start >= 0 && end > start ? command[(start + 1)..end] : command.Trim();
}

static string ReadHeader(string message, string header)
{
    foreach (var raw in message.Replace("\r\n", "\n").Split('\n'))
    {
        if (raw.Length == 0) break; // headers end at the first blank line
        if (raw.StartsWith(header + ":", StringComparison.OrdinalIgnoreCase))
            return raw[(header.Length + 1)..].Trim();
    }
    return "(sem assunto)";
}
