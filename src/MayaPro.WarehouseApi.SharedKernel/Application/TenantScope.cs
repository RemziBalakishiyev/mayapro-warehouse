namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// A scoped, explicit tenant override for execution paths that legitimately have no JWT.
/// <para>
/// Exactly one such path exists today: the anonymous public invoice link
/// (<c>GET /api/public/invoices/{token}</c>). The token itself identifies the sale, and the sale identifies
/// the tenant, so the handler resolves the tenant from the token and then runs the ordinary, fully filtered
/// invoice pipeline inside <see cref="Use"/>. Nothing is bypassed: the rest of the request is scoped to the
/// tenant the token belongs to.
/// </para>
/// <para>
/// <see cref="ICurrentTenant"/> implementations consult this override before the JWT claim. The scope is
/// per DI scope (i.e. per request) and single-use by design — nesting is not supported and returning from
/// the <see cref="IDisposable"/> restores "no override".
/// </para>
/// </summary>
public sealed class TenantScope
{
    /// <summary>The overriding tenant id, or <c>null</c> when the JWT claim should be used.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Enters the override; disposing the returned handle leaves it.</summary>
    public IDisposable Use(Guid tenantId)
    {
        Guid? previous = TenantId;
        TenantId = tenantId;
        return new Handle(this, previous);
    }

    private sealed class Handle(TenantScope owner, Guid? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            owner.TenantId = previous;
        }
    }
}
