namespace MayaPro.WarehouseApi.SharedKernel.Domain;

/// <summary>
/// Base type for all domain entities. Provides identity and audit timestamps.
/// Timestamps are maintained automatically by <c>AuditInterceptor</c>.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
