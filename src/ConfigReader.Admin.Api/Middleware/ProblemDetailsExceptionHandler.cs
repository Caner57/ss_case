using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ConfigReader.Admin.Api.Middleware;

/// <summary>
/// Terminal handler for unhandled exceptions. It logs the full detail internally (with a
/// correlation id) but returns only a generic ProblemDetails to the caller — no stack trace,
/// exception message or connection string ever reaches the response, which would otherwise leak
/// internal system detail. The correlation id lets an operator tie a report back to the log entry.
/// </summary>
public static class ProblemDetailsExceptionHandler
{
    public static async Task HandleAsync(HttpContext context)
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var correlationId = context.TraceIdentifier;

        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(ProblemDetailsExceptionHandler));
        logger.LogError(feature?.Error, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "The request could not be processed. Contact support with the correlation id if it persists."
        };
        problem.Extensions["correlationId"] = correlationId;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
