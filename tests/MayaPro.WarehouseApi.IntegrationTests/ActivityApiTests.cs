using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// End-to-end test that the real <c>DbActivityLogger</c> works: an operation in a chain (a sale) leaves a
/// real activity entry that shows up in the feed.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ActivityApiTests : IAsyncLifetime
{
    private readonly WarehouseApiFactory _factory;

    public ActivityApiTests(WarehouseApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.EnsureDatabaseResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Sale_Writes_A_Real_Activity_Entry()
    {
        HttpClient client = await _factory.AuthenticatedClientAsync();
        var product = await client.CreateProductAsync("ACT-SALE", quantity: 10, salePrice: 10m);

        await client.PostAsJsonAsync("/api/sales", new
        {
            productId = product.Id,
            quantity = 2,
            salePrice = 10m,
            discount = 0m,
            paymentType = "Nağd",
            customerId = (Guid?)null
        });

        List<IntegrationTestHelpers.ActivityDto> feed =
            (await client.GetFromJsonAsync<List<IntegrationTestHelpers.ActivityDto>>("/api/activity?take=50"))!;

        Assert.NotEmpty(feed);
        Assert.Contains(feed, a => a.Action == "Satış etdi");
    }
}
