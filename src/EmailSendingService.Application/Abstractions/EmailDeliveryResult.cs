namespace EmailSendingService.Application.Abstractions;

/// <summary>Outcome of a transport-level send operation.</summary>
public sealed record EmailDeliveryResult(
    bool Success,
    string ProviderMessageId,
    IReadOnlyList<string> ServerReplies)
{
    public static EmailDeliveryResult Ok(string messageId, IReadOnlyList<string> replies)
        => new(true, messageId, replies);
}
