using EmailSendingService.Application.Emails.Dtos;
using EmailSendingService.Application.Emails.SendEmail;
using Microsoft.AspNetCore.Mvc;

namespace EmailSendingService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class EmailsController : ControllerBase
{
    private readonly SendEmailHandler _handler;

    public EmailsController(SendEmailHandler handler) => _handler = handler;

    /// <summary>Receives an e-mail DTO and dispatches it through the SMTP infrastructure.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SendEmailResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Send([FromBody] SendEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await _handler.HandleAsync(new SendEmailCommand(request), cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return AcceptedAtAction(nameof(Send), result.Value);
    }
}
