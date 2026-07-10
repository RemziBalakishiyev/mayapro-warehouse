using Microsoft.AspNetCore.Diagnostics;

namespace MayaPro.WarehouseApi.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions, logs the detail (English) via Serilog and returns a generic
/// 500 with an Azerbaijani message. Business errors never reach here — they flow through Result.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new { code = "Server.Error", message = "Gözlənilməz xəta baş verdi" },
            cancellationToken);

        return true;
    }
}
