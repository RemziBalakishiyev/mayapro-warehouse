namespace MayaPro.WarehouseApi.SharedKernel.Domain;

/// <summary>
/// Marks an entity as belonging to exactly one tenant (mağaza). Every business row in the system carries
/// this marker; the tenancy registry itself (<c>tenancy.Tenants</c>) deliberately does not.
/// <para>
/// The marker is what drives the whole isolation mechanism, and modules never have to opt in by hand:
/// <list type="bullet">
///   <item>each module's DbContext adds an EF global query filter for every <see cref="ITenantScoped"/>
///   entity in its model (see <c>TenantModelBuilderExtensions</c>), so reads are scoped automatically;</item>
///   <item><c>TenantInterceptor</c> stamps <see cref="TenantId"/> on insert and blocks it from ever being
///   changed afterwards, so writes are scoped automatically.</item>
/// </list>
/// Use case / handler code therefore never mentions <see cref="TenantId"/> — isolation lives entirely in
/// the infrastructure layer.
/// </para>
/// </summary>
public interface ITenantScoped
{
    /// <summary>The owning tenant. Never <c>Guid.Empty</c> on a persisted row.</summary>
    Guid TenantId { get; }
}
