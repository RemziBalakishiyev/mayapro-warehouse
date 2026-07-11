using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests over the real host + SQL Server test database. Verifies the module mechanism
/// (health), the login → token → /me flow, and that protected endpoints reject anonymous callers.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthApiTests : IAsyncLifetime
{
    private const string OwnerPhone = "0501112233";
    private const string DemoPassword = "demo123";

    private readonly WarehouseApiFactory _factory;

    public AuthApiTests(WarehouseApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Health_Returns_200()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_Then_Me_Returns_200_With_User()
    {
        HttpClient client = _factory.CreateClient();

        LoginResponseDto? login = await LoginAsync(client, OwnerPhone, DemoPassword);
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login!.Token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        HttpResponseMessage meResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        UserDto? me = await meResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(me);
        Assert.Equal(OwnerPhone, me!.Phone);
        Assert.Equal("sahib", me.Role);
    }

    [Fact]
    public async Task Me_Without_Token_Returns_401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_400()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new { phone = OwnerPhone, password = "wrong" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ErrorDto? error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.Equal("Auth.InvalidCredentials", error!.Code);
    }

    private static async Task<LoginResponseDto?> LoginAsync(HttpClient client, string phone, string password)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new { phone, password });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LoginResponseDto>();
    }

    private sealed record LoginResponseDto(string Token, UserDto User);

    private sealed record UserDto(Guid Id, string FullName, string Phone, string Role);

    private sealed record ErrorDto(string Code, string Message);
}
