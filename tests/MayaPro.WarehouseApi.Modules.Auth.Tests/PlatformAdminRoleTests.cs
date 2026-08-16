using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using MayaPro.WarehouseApi.SharedKernel.Domain;

namespace MayaPro.WarehouseApi.Modules.Auth.Tests;

/// <summary>
/// BE#36 — the three strings the platform-admin design hangs on. Each is written in one place and read in
/// another that cannot reference it back, so each gets pinned here rather than assumed.
/// </summary>
public sealed class PlatformAdminRoleTests
{
    /// <summary>
    /// <c>TokenService</c> writes the <c>role</c> claim as the enum name; the host's policy and its tenant
    /// gate both match on <see cref="PlatformAdminAccess.RoleName"/>. If the enum were ever renamed, the
    /// admin would silently stop being an admin — and would be refused by the tenant gate instead.
    /// </summary>
    [Fact]
    public void The_Role_Claim_The_Host_Matches_Is_The_Enum_Name()
    {
        Assert.Equal(nameof(UserRole.PlatformAdmin), PlatformAdminAccess.RoleName);
        Assert.Equal(nameof(UserRole.PlatformAdmin), UserRole.PlatformAdmin.ToString());
    }

    /// <summary>The wire code is additive: the three shop roles keep the values the frontend already reads.</summary>
    [Fact]
    public void The_Wire_Codes_Are_Additive_Not_Substituted()
    {
        Assert.Equal("sahib", UserRole.Owner.ToCode());
        Assert.Equal("menecer", UserRole.Manager.ToCode());
        Assert.Equal("satici", UserRole.Seller.ToCode());
        Assert.Equal("platform_admin", UserRole.PlatformAdmin.ToCode());

        Assert.Equal(WireFormat.Roles.PlatformAdmin, RoleCode.PlatformAdmin);
    }

    /// <summary>Every role has a wire code — <c>ToCode</c> must never throw for a value the enum defines.</summary>
    [Fact]
    public void Every_Role_Maps_To_A_Code()
    {
        foreach (UserRole role in Enum.GetValues<UserRole>())
            Assert.False(string.IsNullOrWhiteSpace(role.ToCode()));
    }

    /// <summary>
    /// The reserved tenant id is not empty and is not the default shop — the two values it must never
    /// collide with. Empty would be read as "unset" by <c>TenantInterceptor</c>; the default shop would put
    /// the platform admin inside a real customer's data.
    /// </summary>
    [Fact]
    public void The_Platform_Tenant_Id_Collides_With_Nothing()
    {
        Assert.NotEqual(Guid.Empty, TenantDefaults.PlatformTenantId);
        Assert.NotEqual(TenantDefaults.DefaultTenantId, TenantDefaults.PlatformTenantId);
    }
}
