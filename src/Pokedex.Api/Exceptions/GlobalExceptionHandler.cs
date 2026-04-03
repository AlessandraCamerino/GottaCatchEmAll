using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Pokedex.Api.Exceptions;

// Centralised exception-to-HTTP mapping.
//
// Design decision: rather than repeating try/catch in every controller action,
// we register a single IExceptionHandler. ASP.NET Core's UseExceptionHandler() middleware
// invokes this handler for any unhandled exception in the pipeline.
//
// Adding a new exception type only requires adding a case here — controllers remain unchanged.
public class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        var (statusCode, title, detail) = exception switch
        {
            // For known domain exceptions, expose the message — it is safe and user-facing.
            PokemonNotFoundException => (StatusCodes.Status404NotFound, "Not Found", exception.Message),

            // For unexpected errors, return a generic message.
            // Never expose internal exception messages or stack traces to clients.
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        context.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            }
        });
    }
}
