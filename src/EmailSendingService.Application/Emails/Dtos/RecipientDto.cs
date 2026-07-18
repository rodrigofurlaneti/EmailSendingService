namespace EmailSendingService.Application.Emails.Dtos;

public sealed class RecipientDto
{
    public string Address { get; set; } = string.Empty;
    public string? Name { get; set; }
}
