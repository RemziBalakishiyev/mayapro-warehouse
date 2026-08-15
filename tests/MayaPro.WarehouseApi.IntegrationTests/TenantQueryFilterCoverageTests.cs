using MayaPro.WarehouseApi.Modules.Activity.Infrastructure;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using MayaPro.WarehouseApi.Modules.Customers.Infrastructure;
using MayaPro.WarehouseApi.Modules.DayEnd.Infrastructure;
using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;
using MayaPro.WarehouseApi.Modules.Products.Infrastructure;
using MayaPro.WarehouseApi.Modules.Sales.Infrastructure;
using MayaPro.WarehouseApi.Modules.Settings.Infrastructure;
using MayaPro.WarehouseApi.Modules.Suppliers.Infrastructure;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// AC-6 / TC-28 — the safety net for the whole feature. It walks every module's EF model and fails if any
/// tenant-scoped entity is missing its global query filter or its tenant index.
/// <para>
/// This is what makes the isolation guarantee survive future work: a new table added without deriving from
/// <see cref="TenantEntity"/> shows up here as a red test rather than as a silent cross-tenant leak years
/// later. Only the model is built — no database is touched.
/// </para>
/// </summary>
public sealed class TenantQueryFilterCoverageTests
{
    public static TheoryData<string, Func<IModel>> Models() => new()
    {
        { "identity", () => Model<AuthDbContext>(o => new AuthDbContext(o)) },
        { "products", () => Model<ProductsDbContext>(o => new ProductsDbContext(o)) },
        { "sales", () => Model<SalesDbContext>(o => new SalesDbContext(o)) },
        { "customers", () => Model<CustomersDbContext>(o => new CustomersDbContext(o)) },
        { "suppliers", () => Model<SuppliersDbContext>(o => new SuppliersDbContext(o)) },
        { "expenses", () => Model<ExpensesDbContext>(o => new ExpensesDbContext(o)) },
        { "dayend", () => Model<DayEndDbContext>(o => new DayEndDbContext(o)) },
        { "activity", () => Model<ActivityDbContext>(o => new ActivityDbContext(o)) },
        { "settings", () => Model<SettingsDbContext>(o => new SettingsDbContext(o)) }
    };

    [Theory]
    [MemberData(nameof(Models))]
    public void Every_Tenant_Scoped_Entity_Has_A_Query_Filter(string schema, Func<IModel> model)
    {
        List<IEntityType> tenantScoped = model()
            .GetEntityTypes()
            .Where(e => typeof(ITenantScoped).IsAssignableFrom(e.ClrType))
            .ToList();

        Assert.NotEmpty(tenantScoped);

        foreach (IEntityType entity in tenantScoped)
        {
            Assert.True(
                entity.GetQueryFilter() is not null,
                $"{schema}.{entity.ClrType.Name} tenant-scoped-dir, amma global query filter-i yoxdur");

            Assert.True(
                entity.GetIndexes().Any(i => i.Properties.Any(p => p.Name == nameof(ITenantScoped.TenantId))),
                $"{schema}.{entity.ClrType.Name} üçün TenantId indeksi yoxdur");
        }
    }

    /// <summary>
    /// The mirror image: the tenant registry itself must <b>not</b> be filtered, or the host could never
    /// look a shop up before it knows which shop the caller belongs to.
    /// </summary>
    [Fact]
    public void The_Tenant_Registry_Itself_Is_Not_Filtered()
    {
        IModel model = Model<TenancyDbContext>(o => new TenancyDbContext(o));

        IEntityType tenant = model.FindEntityType(typeof(Tenant))!;

        Assert.Null(tenant.GetQueryFilter());
        Assert.False(typeof(ITenantScoped).IsAssignableFrom(typeof(Tenant)));
    }

    /// <summary>
    /// Every entity in a module that owns tables is tenant-scoped — there is no "global" business table
    /// hiding outside the mechanism. Guards against someone re-adding one by accident.
    /// </summary>
    [Theory]
    [MemberData(nameof(Models))]
    public void No_Business_Entity_Escapes_The_Tenant_Marker(string schema, Func<IModel> model)
    {
        List<string> unscoped = model()
            .GetEntityTypes()
            .Where(e => !e.IsOwned() && !typeof(ITenantScoped).IsAssignableFrom(e.ClrType))
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.True(unscoped.Count == 0, $"{schema} sxemində tenant-siz entity var: {string.Join(", ", unscoped)}");
    }

    /// <summary>Builds a context's model without opening a connection — the string is never dialled.</summary>
    private static IModel Model<TContext>(Func<DbContextOptions<TContext>, TContext> factory)
        where TContext : DbContext
    {
        DbContextOptions<TContext> options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer("Server=(model-only);Database=none")
            .Options;

        using TContext context = factory(options);
        return context.Model;
    }
}
