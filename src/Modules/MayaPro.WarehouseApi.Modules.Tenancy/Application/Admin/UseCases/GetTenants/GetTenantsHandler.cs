using MayaPro.WarehouseApi.Modules.Tenancy.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Tenancy.Domain;
using MayaPro.WarehouseApi.Modules.Tenancy.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Tenancy.Application.Admin.UseCases.GetTenants;

/// <summary>
/// The platform admin's shop list: status filter plus a free-text search over name / owner / phone, with
/// the billing summary each row shows (last payment, total paid, whether the period has lapsed).
/// <para>
/// <b>No filter bypass here.</b> Both tables this reads — <c>Tenants</c> and <c>SubscriptionPayments</c> —
/// are platform-level and carry no global query filter at all, so the admin surface needs no
/// <c>IgnoreQueryFilters()</c>. That is the whole point of keeping <c>SubscriptionPayment</c> outside
/// <c>ITenantScoped</c>: the module that is allowed to see every shop is also the module with nothing to
/// bypass.
/// </para>
/// </summary>
public sealed class GetTenantsHandler(TenancyDbContext db, IDateProvider dateProvider)
{
    /// <summary>The <c>ESCAPE</c> character the search pattern below is built with.</summary>
    private const string LikeEscape = "\\";

    public async Task<Result<IReadOnlyList<TenantListItemDto>>> Handle(GetTenantsQuery query, CancellationToken ct)
    {
        TenantStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse(query.Status, ignoreCase: true, out TenantStatus parsed))
                return Result.Failure<IReadOnlyList<TenantListItemDto>>(
                    Error.Validation("Naməlum mağaza statusu"));

            status = parsed;
        }

        IQueryable<Tenant> tenants = db.Tenants.AsNoTracking();

        if (status is { } wanted)
            tenants = tenants.Where(t => t.Status == wanted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // BE#40 — there is deliberately no ToLower() on this path, on either side.
            //
            // The host culture is az-Latn-AZ, where 'I'.ToLower() is 'ı' (U+0131) while SQL Server's
            // LOWER() maps 'I' to 'i'. Lowering the term in C# and the column in SQL therefore produced
            // two different strings for any term containing a capital I, and the comparison could never
            // match — "QAAXTARIS" found nothing while "qaaxtaris" found the row.
            //
            // The fix is to stop translating case at all: LIKE resolves case (and accents) through the
            // column's collation, which is case-insensitive, so the same term matches in any register
            // without a culture ever entering the picture.
            string term = query.Search.Trim();
            string pattern = $"%{EscapeLikePattern(term)}%";

            // BE#51 — since BE#46, Phone is stored canonically (994501234567), so an admin typing the
            // number the way a human would (0501234567, +994 50 123 45 67, 050 123-45-67, …) never
            // matches the raw LIKE above. PhoneNormalizer knows every shape a *complete* phone can take,
            // so if the term canonicalizes we also match on the exact canonical value.
            //
            // A term that is not a full phone number — a name/e-mail that happens to contain digits, or a
            // deliberate partial fragment such as "5012345" — fails to canonicalize (wrong digit count) and
            // simply falls through to the LIKE fallback below, exactly as before this fix.
            string? canonicalPhone = PhoneNormalizer.Normalize(term) is { IsSuccess: true } normalized
                ? normalized.Value
                : null;

            tenants = tenants.Where(t =>
                EF.Functions.Like(t.Name, pattern, LikeEscape) ||
                (t.OwnerName != null && EF.Functions.Like(t.OwnerName, pattern, LikeEscape)) ||
                (t.Phone != null && (EF.Functions.Like(t.Phone, pattern, LikeEscape) ||
                                      (canonicalPhone != null && t.Phone == canonicalPhone))));
        }

        List<Tenant> rows = await tenants.OrderBy(t => t.Name).ToListAsync(ct);

        List<Guid> ids = rows.Select(t => t.Id).ToList();

        // Platform scale: tens of shops, hundreds of payment rows. Materialising them and folding in memory
        // keeps "last payment amount" (which SQL cannot express as a plain aggregate) readable and correct.
        List<SubscriptionPayment> payments = await db.SubscriptionPayments
            .AsNoTracking()
            .Where(p => ids.Contains(p.TenantId))
            .ToListAsync(ct);

        Dictionary<Guid, List<SubscriptionPayment>> byTenant = payments
            .GroupBy(p => p.TenantId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PaidAt).ToList());

        DateTime now = dateProvider.UtcNow;

        List<TenantListItemDto> items = rows
            .Select(t =>
            {
                byTenant.TryGetValue(t.Id, out List<SubscriptionPayment>? paid);
                SubscriptionPayment? last = paid?.FirstOrDefault();

                return new TenantListItemDto(
                    t.Id,
                    t.Name,
                    t.OwnerName,
                    t.Phone,
                    t.Status.ToString(),
                    t.ExpiresAt,
                    t.MonthlyFee,
                    t.IsSubscriptionExpired(now),
                    last?.PaidAt,
                    last?.Amount,
                    paid?.Sum(p => p.Amount) ?? 0m);
            })
            .ToList();

        return Result.Success<IReadOnlyList<TenantListItemDto>>(items);
    }

    /// <summary>
    /// The search box is free text, so LIKE's own metacharacters must be neutralised: without this a
    /// lone <c>%</c> would list every shop on the platform and <c>_</c> would silently match any letter.
    /// The backslash prefix is honoured because every call passes it as the <c>ESCAPE</c> character.
    /// </summary>
    private static string EscapeLikePattern(string term) => term
        .Replace(LikeEscape, LikeEscape + LikeEscape, StringComparison.Ordinal)
        .Replace("%", LikeEscape + "%", StringComparison.Ordinal)
        .Replace("_", LikeEscape + "_", StringComparison.Ordinal)
        .Replace("[", LikeEscape + "[", StringComparison.Ordinal);
}
