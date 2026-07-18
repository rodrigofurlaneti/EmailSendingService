using System.Net;
using System.Text.Json;
using EmailSendingService.Infrastructure.Smtp;

namespace EmailSendingService.Api.Middleware;

/// <summary>Converts unhandled exceptions into RFC 7807-style JSON problem responses.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP transport failure.");
            await WriteProblem(context, HttpStatusCode.BadGateway, "SMTP transport error", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while processing the request.");
            await WriteProblem(context, HttpStatusCode.InternalServerError, "Unexpected error", ex.Message);
        }
    }

    private static async Task WriteProblem(HttpContext context, HttpStatusCode status, string title, string detail)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";
        var payload = JsonSerializer.Serialize(new
        {
            type = $"https://httpstatuses.com/{(int)status}",
            title,
            status = (int)status,
            detail
        });
        await context.Response.WriteAsync(payload);
    }
}
