using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using EmailSendingService.Infrastructure.Configuration;

namespace EmailSendingService.Infrastructure.Smtp;

/// <summary>
/// Owns the raw TCP socket and optional TLS upgrade. It builds a
/// <see cref="SmtpSession"/> over the live network stream — this is the only
/// place that touches sockets, keeping the protocol logic transport-agnostic.
/// </summary>
public sealed class SmtpTransport : IAsyncDisposable
{
    private readonly SmtpSettings _settings;
    private TcpClient? _tcp;
    private Stream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public SmtpTransport(SmtpSettings settings) => _settings = settings;

    public SmtpSession Session { get; private set; } = null!;

    /// <summary>Connects, performs greeting + EHLO, upgrades to TLS and authenticates.</summary>
    public async Task<IReadOnlyList<string>> OpenAsync(CancellationToken ct = default)
    {
        var transcript = new List<string>();

        _tcp = new TcpClient { SendTimeout = _settings.TimeoutMilliseconds, ReceiveTimeout = _settings.TimeoutMilliseconds };
        try
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(_settings.TimeoutMilliseconds);
            await _tcp.ConnectAsync(_settings.Host, _settings.Port, connectCts.Token);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            throw new SmtpException(
                $"Não foi possível conectar ao servidor SMTP em {_settings.Host}:{_settings.Port}. " +
                "No modo Relay, confirme que o servidor SMTP (ex.: o SMTP Catcher em tools/EmailSendingService.SmtpCatcher) " +
                "está rodando e escutando nessa porta. No modo DirectMx, o servidor de destino recusou a conexão ou a porta 25 está bloqueada.",
                inner: ex);
        }

        _stream = _tcp.GetStream();

        if (_settings.UseImplicitTls)
            _stream = await UpgradeToTlsAsync(_stream, ct);

        BuildSession(_stream);

        var greeting = await Session.ReadReplyAsync(ct);
        transcript.Add(greeting.ToString());
        if (!greeting.IsPositiveCompletion)
            throw new SmtpException($"Unexpected greeting: {greeting}", greeting.Code);

        var ehlo = await Session.EhloAsync(_settings.ClientHostName, ct);
        transcript.Add(ehlo.ToString());

        var supportsStartTls = ehlo.Lines.Any(l => l.StartsWith("STARTTLS", StringComparison.OrdinalIgnoreCase));

        if (!_settings.UseImplicitTls && _settings.UseStartTls && supportsStartTls)
        {
            await Session.SendCommandAsync("STARTTLS", r => r.IsPositiveCompletion, ct);
            _stream = await UpgradeToTlsAsync(_stream, ct);
            BuildSession(_stream);
            var ehloTls = await Session.EhloAsync(_settings.ClientHostName, ct);
            transcript.Add(ehloTls.ToString());
        }

        if (!string.IsNullOrEmpty(_settings.Username))
        {
            await Session.AuthLoginAsync(_settings.Username!, _settings.Password ?? string.Empty, ct);
            transcript.Add("AUTH LOGIN -> 235 Authenticated");
        }

        return transcript;
    }

    private void BuildSession(Stream stream)
    {
        // Leave the underlying stream open so a later STARTTLS upgrade can reuse it.
        _reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = false, NewLine = "\r\n" };
        Session = new SmtpSession(_reader, _writer);
    }

    private async Task<Stream> UpgradeToTlsAsync(Stream inner, CancellationToken ct)
    {
        var ssl = new SslStream(inner, leaveInnerStreamOpen: false, ValidateCertificate);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = _settings.Host
        }, ct);
        return ssl;
    }

    private bool ValidateCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
        => _settings.AllowInvalidCertificates || errors == SslPolicyErrors.None;

    public async ValueTask DisposeAsync()
    {
        try { if (_writer is not null) await _writer.DisposeAsync(); } catch { /* ignore */ }
        try { _reader?.Dispose(); } catch { /* ignore */ }
        try { if (_stream is not null) await _stream.DisposeAsync(); } catch { /* ignore */ }
        _tcp?.Dispose();
    }
}
