namespace EmailSendingService.Infrastructure.Dns;

/// <summary>Resolves the mail exchangers (MX) for a domain, ordered by preference.</summary>
public interface IMxResolver
{
    Task<IReadOnlyList<string>> ResolveAsync(string domain, CancellationToken cancellationToken = default);
}
