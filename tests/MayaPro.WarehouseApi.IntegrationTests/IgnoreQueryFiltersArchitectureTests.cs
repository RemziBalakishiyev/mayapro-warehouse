using System.Text.RegularExpressions;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// BE#36, AC-7 — the bypass guard. Tenant isolation is enforced by EF global query filters, so
/// <c>IgnoreQueryFilters()</c> is the one call that can switch it off. This test reads every C# source file
/// under <c>src/</c> and fails unless each call site is on the allowlist below, together with the reason it
/// is allowed.
/// <para>
/// It deliberately reads <b>source</b>, not IL: what needs governing is the human act of writing the call,
/// and a reviewer must be able to see the justification next to the file name. The list is exact — an entry
/// that no longer contains a call fails too, so it cannot quietly rot into a list of historical trivia.
/// </para>
/// <para>
/// Note what is <b>not</b> here: the Tenancy module's admin use cases. They read every shop, yet need no
/// bypass at all, because neither <c>Tenant</c> nor <c>SubscriptionPayment</c> is <c>ITenantScoped</c> —
/// they are platform-level tables with no filter to ignore. Keeping the payment ledger out of
/// <c>ITenantScoped</c> is what let the admin surface stay bypass-free.
/// </para>
/// </summary>
public sealed class IgnoreQueryFiltersArchitectureTests
{
    /// <summary>
    /// Every place the tenant filter may legitimately be switched off, with the reason. Paths are relative
    /// to the repository root and use forward slashes.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Allowed = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["src/Modules/MayaPro.WarehouseApi.Modules.Auth/Application/UseCases/Login/LoginHandler.cs"] =
            "§4.1 — login is anonymous, so there is no tenant yet; the lookup is by phone only and an "
            + "ambiguous match is refused rather than resolved.",

        ["src/Modules/MayaPro.WarehouseApi.Modules.Auth/Application/IdentityProvisioningContract.cs"] =
            "§4.1 (BE#36) — registration is anonymous and the phone must be checked across ALL shops; "
            + "returns a bool from Any(), reads no row, exposes no field.",

        ["src/Modules/MayaPro.WarehouseApi.Modules.Sales/Application/SalesModuleContract.cs"] =
            "§4.2 — the anonymous invoice link resolves its tenant from the token; returns only "
            + "(SaleId, TenantId) and the rest of the request runs fully filtered inside TenantScope.",

        ["src/Modules/MayaPro.WarehouseApi.Modules.Auth/Infrastructure/UserSeeder.cs"] =
            "§4.3 — startup seeder: no tenant context, and a filtered emptiness check would re-seed on "
            + "every boot.",

        ["src/Modules/MayaPro.WarehouseApi.Modules.Auth/Infrastructure/PlatformAdminSeeder.cs"] =
            "§4.3 (BE#36) — startup seeder for the platform admin; the same emptiness-check exception.",

        ["src/Modules/MayaPro.WarehouseApi.Modules.Products/Infrastructure/ProductSeeder.cs"] =
            "§4.3 — startup seeder (products and categories).",

        ["src/Modules/MayaPro.WarehouseApi.Modules.Customers/Infrastructure/CustomerSeeder.cs"] =
            "§4.3 — startup seeder.",

        ["src/Modules/MayaPro.WarehouseApi.Modules.Suppliers/Infrastructure/SupplierSeeder.cs"] =
            "§4.3 — startup seeder.",

        ["src/Modules/MayaPro.WarehouseApi.Modules.Expenses/Infrastructure/ExpenseTypeSeeder.cs"] =
            "§4.3 — startup seeder."
    };

    /// <summary>An actual call, not a mention of the name in a doc comment.</summary>
    private static readonly Regex CallSite = new(@"\.IgnoreQueryFilters\s*\(", RegexOptions.Compiled);

    [Fact]
    public void No_Undeclared_Query_Filter_Bypass_Exists_In_The_Source()
    {
        List<string> found = FilesWithBypass();

        List<string> undeclared = found.Where(path => !Allowed.ContainsKey(path)).ToList();

        Assert.True(
            undeclared.Count == 0,
            "Tenant filtri icazəsiz yerdə söndürülüb (IgnoreQueryFilters). Ya çağırışı silin, ya da "
            + $"əsaslandırma ilə allowlist-ə əlavə edin:{Environment.NewLine}"
            + string.Join(Environment.NewLine, undeclared));
    }

    /// <summary>
    /// The other direction: the allowlist may not outlive the code. A path that no longer exists — or that
    /// exists but no longer bypasses anything — is stale and must be removed, or the list stops meaning
    /// "these are all of them".
    /// </summary>
    [Fact]
    public void The_Allowlist_Contains_No_Stale_Entries()
    {
        List<string> found = FilesWithBypass();

        List<string> stale = Allowed.Keys
            .Where(path => !found.Contains(path, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            stale.Count == 0,
            "Allowlist köhnəlib — bu yollarda artıq IgnoreQueryFilters çağırışı yoxdur (və ya fayl "
            + $"mövcud deyil):{Environment.NewLine}{string.Join(Environment.NewLine, stale)}");
    }

    /// <summary>Sanity check on the scanner itself: if it finds nothing, it is broken, not the code.</summary>
    [Fact]
    public void The_Scanner_Actually_Reads_The_Source_Tree()
    {
        DirectoryInfo root = RepositoryRoot();
        List<string> sources = SourceFiles(root).ToList();

        Assert.True(sources.Count > 100, $"src/ altında yalnız {sources.Count} .cs faylı tapıldı — skaner yanlış qovluğa baxır");
        Assert.NotEmpty(FilesWithBypass());
    }

    private static List<string> FilesWithBypass()
    {
        DirectoryInfo root = RepositoryRoot();

        return SourceFiles(root)
            .Where(HasCallSite)
            .Select(path => Relative(root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static bool HasCallSite(string path) =>
        File.ReadLines(path).Any(line =>
        {
            string trimmed = line.TrimStart();
            // Doc comments mention the method by name all over this codebase; only real code counts.
            if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith("*", StringComparison.Ordinal))
                return false;

            return CallSite.IsMatch(line);
        });

    private static IEnumerable<string> SourceFiles(DirectoryInfo root) =>
        Directory.EnumerateFiles(Path.Combine(root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            // Build output and generated obj/ artefacts are not source anybody wrote.
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj", StringComparison.OrdinalIgnoreCase)
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin", StringComparison.OrdinalIgnoreCase));

    private static string Relative(DirectoryInfo root, string path) =>
        Path.GetRelativePath(root.FullName, path).Replace('\\', '/');

    /// <summary>
    /// Walks up from the test assembly until the solution file appears. Nothing in the test host knows the
    /// repository layout, and hard-coding "../../../../.." would break the moment the target framework
    /// folder changes.
    /// </summary>
    private static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MayaPro.WarehouseApi.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!;
    }
}
