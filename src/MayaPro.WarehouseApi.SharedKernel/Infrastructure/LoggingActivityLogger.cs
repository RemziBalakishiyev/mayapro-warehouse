using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.Extensions.Logging;

namespace MayaPro.WarehouseApi.SharedKernel.Infrastructure;

/// <summary>
/// Temporary <see cref="IActivityLogger"/> implementation: writes each activity entry to the logging
/// pipeline (Serilog) instead of persisting it. When the real Activity module arrives, it replaces this
/// registration and starts writing to the <c>activity</c> schema — no calling code changes.
/// </summary>
public sealed class LoggingActivityLogger(ILogger<LoggingActivityLogger> logger) : IActivityLogger
{
    public Task LogAsync(string type, string message, Guid? userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Activity {ActivityType} by {UserId}: {ActivityMessage}",
            type,
            userId?.ToString() ?? "anonymous",
            message);

        return Task.CompletedTask;
    }
}
