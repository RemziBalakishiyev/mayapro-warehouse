using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MayaPro.WarehouseApi.Modules.Auth.Infrastructure;

/// <summary>
/// BE#36 — creates the single <see cref="UserRole.PlatformAdmin"/> account from the <c>PlatformAdmin</c>
/// configuration section. Runs in <b>every</b> environment (unlike the demo <c>UserSeeder</c>): without it a
/// fresh production install would have no way into <c>/api/admin/*</c> at all.
/// <list type="bullet">
///   <item><b>Idempotent.</b> If a platform admin already exists, nothing happens — no second account, and
///   an existing admin's password is never rewritten from configuration.</item>
///   <item><b>Configuration-driven.</b> Phone and password come from <see cref="PlatformAdminOptions"/>
///   (override with <c>PlatformAdmin__Password</c> in production). Nothing is seeded when they are unset, so
///   an installation cannot inherit a guessable default.</item>
///   <item><b>Tenant.</b> The row carries <see cref="TenantDefaults.PlatformTenantId"/>, a reserved id no
///   shop uses — see that constant for why this beats both <c>NULL</c> and <c>Guid.Empty</c>.</item>
/// </list>
/// <para>
/// The existence check runs with <c>IgnoreQueryFilters()</c>: startup has no tenant context, so the filtered
/// query would see nothing and re-seed on every boot. Same documented exception as every other seeder.
/// </para>
/// </summary>
public sealed class PlatformAdminSeeder(
    IAuthDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<PlatformAdminOptions> options,
    ILogger<PlatformAdminSeeder> logger)
{
    /// <summary>The configuration key named in the BE#52 start-up warning, kept as a constant so the log
    /// message and any test asserting on it cannot drift apart.</summary>
    public const string PhoneConfigurationKey = "PlatformAdmin:Phone";

    /// <summary>How many trailing characters of a rejected phone survive in the log — everything before them
    /// is replaced with <c>*</c>, so an operator's near-complete number never lands in plaintext logs.</summary>
    private const int VisibleSuffixLength = 3;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        PlatformAdminOptions settings = options.Value;
        if (!settings.IsConfigured)
            return;

        bool exists = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Role == UserRole.PlatformAdmin, ct);

        if (exists)
            return;

        // BE#46 — the configured phone is stored canonically, so the admin can sign in typing it any way they
        // like. A configured value that cannot be canonicalized is seeded verbatim rather than crashing every
        // boot: a mistyped operator phone should not take a whole installation offline, and login will simply
        // never match it — which is visible, fixable and strictly better than a start-up loop.
        Result<string> canonicalPhone = PhoneNormalizer.Normalize(settings.Phone);

        if (canonicalPhone.IsFailure)
        {
            // BE#52 — the verbatim fallback above means the seeded admin's phone will never match what
            // LoginHandler normalizes at sign-in time, silently producing an unusable account. Nothing else
            // observes that failure, so it is logged here — the one diagnostic signal an operator gets.
            logger.LogWarning(
                "{ConfigKey} could not be normalized (value ending in \"{MaskedPhone}\"): the platform admin " +
                "account will be seeded with this value verbatim and will not be able to log in, because " +
                "login normalizes the phone it is given before comparing it.",
                PhoneConfigurationKey,
                MaskPhone(settings.Phone));
        }

        User admin = User.Create(
            settings.FullName,
            canonicalPhone.IsSuccess ? canonicalPhone.Value : settings.Phone.Trim(),
            email: null,
            passwordHasher.Hash(settings.Password),
            UserRole.PlatformAdmin);

        admin.AssignTenant(TenantDefaults.PlatformTenantId);

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Masks a rejected phone value for logging: every character except the last <see cref="VisibleSuffixLength"/>
    /// becomes <c>*</c>. Short values (at or below that length) are masked entirely, so a one- or two-character
    /// misconfiguration never shows up unmasked.
    /// </summary>
    private static string MaskPhone(string? raw)
    {
        string value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return "(boş)";

        if (value.Length <= VisibleSuffixLength)
            return new string('*', value.Length);

        string suffix = value[^VisibleSuffixLength..];
        return new string('*', value.Length - VisibleSuffixLength) + suffix;
    }
}
