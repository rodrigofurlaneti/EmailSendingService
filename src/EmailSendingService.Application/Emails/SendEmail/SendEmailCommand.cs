using EmailSendingService.Application.Emails.Dtos;

namespace EmailSendingService.Application.Emails.SendEmail;

public sealed record SendEmailCommand(SendEmailRequest Request);
