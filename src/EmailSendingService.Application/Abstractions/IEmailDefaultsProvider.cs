namespace EmailSendingService.Application.Abstractions;

/// <summary>
/// Supplies the default sender identity, configured by infrastructure. Keeps the
/// Application layer free of any knowledge about configuration sources.
/// </summary>
public interface IEmailDefaultsProvider
{
    string? DefaultFromAddress { get; }
    string? DefaultFromName { get; }
}
