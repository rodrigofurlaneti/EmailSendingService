namespace EmailSendingService.Infrastructure.Configuration;

/// <summary>
/// DKIM signing configuration. When enabled, every outgoing message is signed
/// with an RSA private key so receiving servers can verify the message via the
/// public key published in DNS (selector._domainkey.domain).
/// </summary>
public sealed class DkimOptions
{
    /// <summary>Turns DKIM signing on/off. Off by default.</summary>
    public bool Enabled { get; set; }

    /// <summary>The signing domain (d= tag), e.g. "seudominio.com".</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>The selector (s= tag), e.g. "default" -> default._domainkey.seudominio.com.</summary>
    public string Selector { get; set; } = "default";

    /// <summary>RSA private key in PEM (PKCS#8 or PKCS#1). Keep it secret.</summary>
    public string PrivateKeyPem { get; set; } = string.Empty;

    /// <summary>Path to a .pem file holding the private key (alternative to inline PrivateKeyPem).</summary>
    public string? PrivateKeyPath { get; set; }
}
