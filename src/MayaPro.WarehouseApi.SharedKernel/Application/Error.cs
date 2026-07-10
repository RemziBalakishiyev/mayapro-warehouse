namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// A business error: a stable machine code plus a user-facing message (Azerbaijani).
/// Business rule violations return an <see cref="Error"/> via <see cref="Result"/> rather than throwing.
/// </summary>
public sealed record Error(string Code, string Message)
{
    /// <summary>Represents the absence of an error.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>A generic "not found" error; modules may define more specific variants.</summary>
    public static Error NotFound(string message) => new("General.NotFound", message);

    /// <summary>A generic conflict error; modules may define more specific variants.</summary>
    public static Error Conflict(string message) => new("General.Conflict", message);

    /// <summary>A generic validation error; modules may define more specific variants.</summary>
    public static Error Validation(string message) => new("General.Validation", message);
}
