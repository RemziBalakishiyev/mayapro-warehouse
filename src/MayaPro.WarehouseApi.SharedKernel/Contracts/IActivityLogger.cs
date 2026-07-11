namespace MayaPro.WarehouseApi.SharedKernel.Contracts;

/// <summary>
/// Cross-module activity log. Every module writes user-facing events through this contract instead of
/// touching another module's tables. Until the dedicated Activity module ships, a temporary logging
/// implementation records events via Serilog — only the implementation changes later, not the callers.
/// </summary>
public interface IActivityLogger
{
    /// <summary>
    /// Records an activity entry.
    /// </summary>
    /// <param name="type">Short action label (Azerbaijani), e.g. "Mal əlavə etdi", "Stok dəyişdi".</param>
    /// <param name="message">Human-readable detail (Azerbaijani).</param>
    /// <param name="userId">The acting user, or <c>null</c> when the caller is anonymous/unknown.</param>
    Task LogAsync(string type, string message, Guid? userId, CancellationToken cancellationToken = default);
}
