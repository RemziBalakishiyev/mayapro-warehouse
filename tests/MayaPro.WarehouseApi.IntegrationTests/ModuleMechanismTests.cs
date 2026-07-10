using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// Boots the real host via WebApplicationFactory. No database is required at this stage —
/// the only module (Identity) has a no-op migration and a static ping endpoint.
/// </summary>
public sealed class ModuleMechanismTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ModuleMechanismTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_Returns_200()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Identity_Ping_Returns_Pong()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/identity/ping");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pong", body.Trim('"'));
    }
}
