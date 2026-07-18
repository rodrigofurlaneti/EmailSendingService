using System.ComponentModel.DataAnnotations;

namespace EmailSendingService.Infrastructure.Configuration;

/// <summary>How the service delivers mail.</summary>
public enum EmailDeliveryMode
{
    /// <summary>Act as an MTA: resolve the recipient's MX via DNS and deliver directly (no external SMTP).</summary>
    DirectMx = 0,

    /// <summary>Relay every message through a configured SMTP server (Host/Port + optional auth).</summary>
    Relay = 1
}

/// <summary>Strongly-typed SMTP configuration, bound from appsettings ("Smtp" section).</summary>
public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    /// <summary>Delivery strategy. Default is DirectMx (the app is its own mail server).</summary>
    public EmailDeliveryMode DeliveryMode { get; set; } = EmailDeliveryMode.DirectMx;

    // ---- Direct MX delivery ----

    /// <summary>DNS server used to resolve MX records in DirectMx mode.</summary>
    public string DnsServer { get; set; } = "8.8.8.8";

    /// <summary>Port used when delivering straight to a recipient's mail exchanger.</summary>
    [Range(1, 65535)]
    public int DirectMxPort { get; set; } = 25;

    // ---- Relay delivery ----

    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 25;

    /// <summary>Upgrade a plaintext connection to TLS via the STARTTLS command (ports 25/587).</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Wrap the socket in TLS immediately on connect (implicit TLS, port 465).</summary>
    public bool UseImplicitTls { get; set; } = false;

    public string? Username { get; set; }
    public string? Password { get; set; }

    public string DefaultFromAddress { get; set; } = string.Empty;
    public string? DefaultFromName { get; set; }

    /// <summary>Value used for EHLO and Message-ID domain when the socket cannot resolve one.</summary>
    public string ClientHostName { get; set; } = "localhost";

    public int TimeoutMilliseconds { get; set; } = 30_000;

    /// <summary>DANGER: accept any TLS certificate. Only for local test servers (e.g. MailHog/Papercut).</summary>
    public bool AllowInvalidCertificates { get; set; } = false;

    /// <summary>DKIM signing options (improves deliverability in DirectMx mode).</summary>
    public DkimOptions Dkim { get; set; } = new();
}
