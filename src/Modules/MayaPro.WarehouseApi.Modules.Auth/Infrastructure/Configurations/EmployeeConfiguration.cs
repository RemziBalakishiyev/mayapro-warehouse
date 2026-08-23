using MayaPro.WarehouseApi.Modules.Auth.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure.Configurations;

/// <summary>
/// BE#57 — <c>identity.Employees</c>, the payroll register. Note what is <b>absent</b>: there is no unique
/// index on <c>Phone</c> (contrast <c>IX_Users_TenantId_Phone</c>), because the number here is a way to reach
/// someone rather than a login identifier. The <c>TenantId</c> index and the global query filter are installed
/// centrally by <c>ApplyTenantIsolation</c>.
/// </summary>
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FullName)
            .IsRequired()
            .HasMaxLength(200);

        // Optional and NOT unique — see the class remarks.
        builder.Property(e => e.Phone)
            .HasMaxLength(30);

        // Free text job title, not a wire role code.
        builder.Property(e => e.Position)
            .IsRequired()
            .HasMaxLength(100);

        // decimal(18,2) — the provider default for decimal, and the money type used everywhere in this
        // codebase. The column default of 0 means "no salary agreed yet"; it is never NULL, so the summary
        // arithmetic never has to special-case a missing figure.
        builder.Property(e => e.MonthlySalary)
            .IsRequired()
            .HasDefaultValue(0m);

        builder.Property(e => e.Note)
            .HasMaxLength(500);

        // Deliberately no HasDefaultValue(true): EF Core would then treat `true` as the property's sentinel
        // and stop sending the column at all, so an employee inserted as inactive would silently arrive
        // active. The domain default (Employee.Create) is the one that matters and it already says true.
        builder.Property(e => e.IsActive)
            .IsRequired();
    }
}
