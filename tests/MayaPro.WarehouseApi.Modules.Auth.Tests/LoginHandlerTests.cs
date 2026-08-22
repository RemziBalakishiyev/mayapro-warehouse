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

    /// <summary>The owner's phone as a person types it.</summary>
    private const string OwnerPhone = "0501112233";

    /// <summary>BE#46 — the same number as <c>identity.Users.Phone</c> stores it.</summary>
    private const string OwnerPhoneCanonical = "994501112233";

    [Fact]
    public async Task Login_With_Correct_Password_Returns_Token()
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: true);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand(OwnerPhone, CorrectPassword), default);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Token));
        Assert.Equal(OwnerPhoneCanonical, result.Value.User.Phone);
        Assert.Equal(RoleCode.Owner, result.Value.User.Role);
    }

    /// <summary>
    /// BE#46, TC-24/TC-25 — the row is stored canonically, and the person signing in may type the number any
    /// way they like. This is the whole point of normalizing on the way in: a shop that registered years ago
    /// as <c>0501112233</c> keeps working when its owner types <c>+994 50 111 22 33</c>.
    /// </summary>
    [Theory]
    [InlineData("0501112233")]
    [InlineData("050 111 22 33")]
    [InlineData("+994 50 111 22 33")]
    [InlineData("994501112233")]
    [InlineData("(050) 111-22-33")]
    public async Task Any_Spelling_Of_The_Stored_Number_Signs_In(string typed)
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: true);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand(typed, CorrectPassword), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(OwnerPhoneCanonical, result.Value.User.Phone);
    }

    /// <summary>
    /// BE#46, TC-26 — a phone that is not a phone gets the ordinary "wrong credentials" refusal, not a format
    /// complaint. Answering differently would tell an attacker which strings are even worth trying.
    /// </summary>
    [Theory]
    [InlineData("12345")]
    [InlineData("501112233")]
    [InlineData("abc")]
    public async Task An_Unparsable_Phone_Gets_The_Neutral_Refusal_Not_A_Format_Error(string typed)
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: true);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand(typed, CorrectPassword), default);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.InvalidCredentials, result.Error);
    }

    /// <summary>BE#46, TC-27 — an empty phone keeps its own, pre-existing validation message.</summary>
    [Fact]
    public async Task An_Empty_Phone_Still_Says_So()
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: true);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand("", CorrectPassword), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Telefon boş ola bilməz", result.Error.Message);
    }

    /// <summary>
    /// BE#46, TC-28 — normalizing does not disturb BE#35: a deactivated account is still told it is
    /// deactivated, whichever spelling was typed.
    /// </summary>
    [Fact]
    public async Task A_Deactivated_Account_Still_Says_So_When_The_Old_Format_Is_Typed()
    {
        await using AuthDbContext db = await CreateDbWithOwnerAsync(isActive: false);
        LoginHandler handler = CreateHandler(db);

        var result = await handler.Handle(new LoginCommand("050 111 22 33", CorrectPassword), default);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UserInactive, result.Error);
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
        db.Users.Add(User.Create("Rəşad Məmmədov", OwnerPhoneCanonical, null, hash, UserRole.Owner, isActive));
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
