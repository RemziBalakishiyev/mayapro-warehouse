namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// BE#36 — the platform operator's own credentials and contact number, bound from the <c>PlatformAdmin</c>
/// configuration section. Nothing here is hard-coded in source: the seeded admin's phone/password come from
/// configuration (override with <c>PlatformAdmin__Password</c> in production), and <see cref="Phone"/> is
/// also the number shown to a shop whose subscription lapsed ("əlaqə: …").
/// <para>
/// It lives in SharedKernel because three unrelated places need it: the Auth module (seeds the admin user,
/// renders the login refusal message), the host's tenant gate (renders the same message per request) and
/// the Tenancy module (nothing yet — kept out on purpose).
/// </para>
/// </summary>
public sealed class PlatformAdminOptions
{
    public const string SectionName = "PlatformAdmin";

    /// <summary>Login phone of the seeded platform admin, and the support number shown to blocked shops.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>Plain password for the seeded admin. Only read at seed time; never stored unhashed.</summary>
    public string Password { get; set; } = string.Empty;

    public string FullName { get; set; } = "Platforma Admini";

    /// <summary>
    /// True when both a phone and a password are configured. Seeding is skipped otherwise — an installation
    /// that has not chosen its operator credentials must not get a guessable admin account by default.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Phone) && !string.IsNullOrWhiteSpace(Password);
}
