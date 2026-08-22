using System.IdentityModel.Tokens.Jwt;
using MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.Login;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MayaPro.WarehouseApi.Modules.Auth.Tests;

public sealed class LoginHandlerTests
{
    private const string CorrectPassword = "demo123";
    private const string OwnerPhone = "0501112233";

    [Fact]
    public async Task Login_With_Correct_Password_Returns_Token()
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: true);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand(OwnerPhone, CorrectPassword), default);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Token));
        Assert.Equal(OwnerPhone, result.Value.User.Phone);
        Assert.Equal(RoleCode.Owner, result.Value.User.Role);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_InvalidCredentials()
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: true);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand(OwnerPhone, "wrong-password"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Login_For_Inactive_User_Returns_UserInactive()
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: false);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand(OwnerPhone, CorrectPassword), default);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UserInactive, result.Error);
    }

    [Fact]
    public async Task Login_With_Unknown_Phone_Returns_InvalidCredentials()
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: true);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand("0000000000", CorrectPassword), default);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    /// <summary>BE#45: omitting <c>rememberMe</c> keeps the short-lived <c>Jwt:ExpiryHours</c> token.</summary>
    [Fact]
    public async Task Login_Without_RememberMe_Issues_Token_Expiring_In_Roughly_24_Hours()
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: true);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand(OwnerPhone, CorrectPassword), default);

        Assert.True(result.IsSuccess);
        DateTime expiry = ReadTokenExpiry(result.Value.Token);
        TimeSpan lifetime = expiry - DateTime.UtcNow;
        Assert.InRange(lifetime.TotalHours, 23.9, 24.1);
    }

    /// <summary>BE#45: <c>rememberMe=true</c> swaps in <c>Jwt:RememberMeExpiryDays</c> (30 days here).</summary>
    [Fact]
    public async Task Login_With_RememberMe_Issues_Token_Expiring_In_Roughly_30_Days()
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: true);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand(OwnerPhone, CorrectPassword, RememberMe: true), default);

        Assert.True(result.IsSuccess);
        DateTime expiry = ReadTokenExpiry(result.Value.Token);
        TimeSpan lifetime = expiry - DateTime.UtcNow;
        Assert.InRange(lifetime.TotalDays, 29.9, 30.1);
    }

    private static DateTime ReadTokenExpiry(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;

    private static readonly BCryptPasswordHasher Hasher = new();

    private static async Task<AuthDbContext> CreateDbWithOwnerAsync(bool isActive)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-tests-{Guid.NewGuid()}")
            .Options;

        var db = new AuthDbContext(options);
        string hash = Hasher.Hash(CorrectPassword);
        db.Users.Add(User.Create("Rəşad Məmmədov", OwnerPhone, null, hash, UserRole.Owner, isActive));
        await db.SaveChangesAsync();
        return db;
    }

    private static LoginHandler CreateHandler(AuthDbContext db)
    {
        var tokenService = new TokenService(Options.Create(new JwtOptions
        {
            Issuer = "test",
            Audience = "test",
            Secret = "unit_test_secret_key_at_least_32_characters_long",
            ExpiryHours = 24,
            RememberMeExpiryDays = 30
        }));

        return new LoginHandler(db, Hasher, tokenService, new LoginValidator());
    }
}
