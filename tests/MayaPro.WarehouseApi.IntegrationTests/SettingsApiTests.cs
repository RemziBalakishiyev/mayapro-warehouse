using System.Net;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end tests for store settings: the singleton is created with defaults on first read, the owner
/// can update it, and a seller cannot.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SettingsApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public SettingsApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_Returns_Defaults_Then_Owner_Update_Persists()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();

        // First read materialises the singleton with defaults.
        var defaults = (await client.GetFromJsonAsync<IntegrationTestHelpers.SettingsDto>("/api/settings"))!;
        Assert.Equal("Sədərək Anbar", defaults.StoreName);
        Assert.Equal("AZN", defaults.Currency);
        Assert.Equal(10, defaults.DefaultMinStock);
        Assert.Equal("az", defaults.Language);
        // Must match the frontend's own default template exactly (single {debt} placeholder).
        Assert.Equal(
            "Salam, sizdə {debt} AZN qalıq borc görünür. Zəhmət olmasa ödənişi tamamlayın.",
            defaults.WhatsappTemplate);
        Assert.Contains("{debt}", defaults.WhatsappTemplate);

        // Owner updates the settings.
        HttpResponseMessage put = await client.PutAsJsonAsync("/api/settings", new
        {
            storeName = "Yeni Mağaza",
            ownerName = "Rəşad",
            whatsappTemplate = "Salam {ad}, borcunuz {borc} AZN.",
            currency = "USD",
            defaultMinStock = 25,
            language = "en"
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = (await put.Content.ReadFromJsonAsync<IntegrationTestHelpers.SettingsDto>())!;
        Assert.Equal("Yeni Mağaza", updated.StoreName);
        Assert.Equal("Rəşad", updated.OwnerName);
        Assert.Equal(25, updated.DefaultMinStock);

        // The change is persisted (still a single row — read reflects the update).
        var reread = (await client.GetFromJsonAsync<IntegrationTestHelpers.SettingsDto>("/api/settings"))!;
        Assert.Equal("Yeni Mağaza", reread.StoreName);
        Assert.Equal("USD", reread.Currency);
        Assert.Equal("en", reread.Language);
    }

    [Fact]
    public async Task Seller_Cannot_Update_Settings_Returns_403()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync(IntegrationTestHelpers.SellerPhone);

        HttpResponseMessage put = await client.PutAsJsonAsync("/api/settings", new
        {
            storeName = "İcazəsiz",
            ownerName = (string?)null,
            whatsappTemplate = "test",
            currency = "AZN",
            defaultMinStock = 5,
            language = "az"
        });

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }
}
