using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MayaPro.WarehouseApi.Modules.Auth.Tests;

/// <summary>
/// BE#52 — the seeder deliberately never fails start-up over a misconfigured <c>PlatformAdmin:Phone</c> (see
/// the seeder's own comment), but a value <see cref="PhoneNormalizer"/> rejects must at least surface as a
/// <see cref="LogLevel.Warning"/>, since the resulting account can never log in (BE#46 normalizes both sides).
/// </summary>
public sealed class PlatformAdminSeederTests
{
    private static PlatformAdminSeeder NewSeeder(
        AuthDbContext db,
        FakeLogger<PlatformAdminSeeder> logger,
        string phone,
        string password = "Str0ngPass!")
    {
        var options = Options.Create(new PlatformAdminOptions
        {
            Phone = phone,
            Password = password,
            FullName = "Platforma Admini"
        });

        return new PlatformAdminSeeder(db, new BCryptPasswordHasher(), options, logger);
    }

    /// <summary>An unnormalizable phone ("admin") still seeds the account (unchanged behaviour, AC-3) but
    /// now logs a warning naming the configuration key and the consequence (AC-1, AC-2).</summary>
    [Fact]
    public async Task Unnormalizable_Phone_Logs_A_Warning_But_Still_Seeds()
    {
        await using AuthDbContext db = AuthTestDb.New();
        var logger = new FakeLogger<PlatformAdminSeeder>();
        PlatformAdminSeeder seeder = NewSeeder(db, logger, phone: "admin");

        await seeder.SeedAsync();

        User admin = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Role == UserRole.PlatformAdmin);
        Assert.Equal("admin", admin.Phone);

        (LogLevel Level, string Message) warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(PlatformAdminSeeder.PhoneConfigurationKey, warning.Message);
        // The raw configured value ("admin") must not appear unmasked — only its masked form ("**min").
        Assert.DoesNotContain("\"admin\"", warning.Message);
        Assert.Contains("**min", warning.Message);
    }

    /// <summary>A too-short numeric value ("12345") is rejected the same way and is masked, not shown verbatim,
    /// in the warning.</summary>
    [Fact]
    public async Task Unnormalizable_Numeric_Phone_Is_Masked_In_The_Warning()
    {
        await using AuthDbContext db = AuthTestDb.New();
        var logger = new FakeLogger<PlatformAdminSeeder>();
        PlatformAdminSeeder seeder = NewSeeder(db, logger, phone: "12345");

        await seeder.SeedAsync();

        (LogLevel Level, string Message) warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain("12345", warning.Message);
        Assert.Contains("**345", warning.Message);
    }

    /// <summary>A normalizable phone seeds cleanly with no extra warning (AC-4).</summary>
    [Fact]
    public async Task Normalizable_Phone_Logs_No_Warning()
    {
        await using AuthDbContext db = AuthTestDb.New();
        var logger = new FakeLogger<PlatformAdminSeeder>();
        PlatformAdminSeeder seeder = NewSeeder(db, logger, phone: "0501234567");

        await seeder.SeedAsync();

        User admin = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Role == UserRole.PlatformAdmin);
        Assert.Equal("994501234567", admin.Phone);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);
    }
}
