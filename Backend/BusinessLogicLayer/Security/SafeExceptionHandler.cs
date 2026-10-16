using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PenielPlaceMinistriesCSharp.Security;

public sealed class SafeExceptionHandler : IExceptionHandler
{
    private readonly ILogger<SafeExceptionHandler> _logger;

    public SafeExceptionHandler(ILogger<SafeExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled server error. RequestId: {RequestId}", httpContext.TraceIdentifier);

        if (httpContext.Response.HasStarted || !WantsJson(httpContext.Request))
            return false;

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "Please try again. If the problem continues, contact the system administrator.",
            Instance = httpContext.Request.Path
        };
        problem.Extensions["requestId"] = httpContext.TraceIdentifier;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static bool WantsJson(HttpRequest request) =>
        request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true) &&
        !request.Headers.Accept.Any(a => a?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true);
}
