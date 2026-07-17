using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>Shared helpers for driving the API in integration tests (login, seed-independent fixtures).</summary>
internal static class IntegrationTestHelpers
{
    public const string OwnerPhone = "0501112233";   // Sahibkar
    public const string SellerPhone = "0553334455";  // Satıcı
    public const string DemoPassword = "demo123";

    public static async Task<HttpClient> AuthenticatedClientAsync(
        this WarehouseApiFactory factory,
        string phone = OwnerPhone,
        string password = DemoPassword)
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login", new { phone, password });
        response.EnsureSuccessStatusCode();
        LoginDto login = (await response.Content.ReadFromJsonAsync<LoginDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
        return client;
    }

    public static async Task<ProductDto> CreateProductAsync(
        this HttpClient client, string barcode, int quantity, decimal salePrice = 10m, string supplierId = "sup_1")
    {
        object body = new
        {
            name = "Satış test malı",
            category = "Test",
            attributes = new[] { new { name = "Ölçü", value = "M" }, new { name = "Rəng", value = "Qara" } },
            barcode,
            image = "",
            note = "",
            purchasePrice = 5m,
            salePrice,
            quantity,
            minStock = 1,
            currency = "AZN",
            supplierId,
            location = "Anbar A / Rəf 1 / Qutu 1",
            store = "Anbar A",
            warehouse = "Anbar A",
            shelf = "1",
            box = "1",
            expenses = new { yol = 0m, fehle = 0m, yer = 0m, paket = 0m, diger = 0m }
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/products", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    public static async Task<CustomerDto> CreateCustomerAsync(
        this HttpClient client, string name, decimal debt = 0m)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/customers", new { name, phone = (string?)null, note = (string?)null, debt });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }

    public static async Task<ProductDto> GetProductAsync(this HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<ProductDto>($"/api/products/{id}"))!;

    public static async Task<CustomerDto> GetCustomerAsync(this HttpClient client, Guid id)
    {
        List<CustomerDto> all = (await client.GetFromJsonAsync<List<CustomerDto>>("/api/customers"))!;
        return all.Single(c => c.Id == id);
    }

    public static async Task<SupplierDto> CreateSupplierAsync(this HttpClient client, string name, decimal debt = 0m)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/suppliers", new
        {
            name,
            contactName = (string?)null,
            phone = (string?)null,
            note = (string?)null,
            debt
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SupplierDto>())!;
    }

    public static async Task<SupplierDto> GetSupplierAsync(this HttpClient client, Guid id)
    {
        List<SupplierDto> all = (await client.GetFromJsonAsync<List<SupplierDto>>("/api/suppliers"))!;
        return all.Single(s => s.Id == id);
    }

    internal sealed record LoginDto(string Token);

    internal sealed record ProductDto(Guid Id, string Name, int Quantity, int InitialQuantity, decimal RealCostPerUnit);

    internal sealed record CustomerDto(Guid Id, string Name, decimal Debt);

    internal sealed record SupplierDto(Guid Id, string Name, decimal Debt, int ItemCount);

    internal sealed record ExpenseDto(Guid Id, string Title, string Category, decimal Amount, Guid? ProductId);

    internal sealed record SaleDto(
        Guid Id,
        Guid? ProductId,
        int Quantity,
        decimal TotalAmount,
        decimal? CostPerUnit,
        decimal? Profit,
        string PaymentType,
        Guid? CustomerId,
        bool IsManual);

    internal sealed record CustomerPaymentDto(Guid Id, Guid CustomerId, decimal Amount);

    internal sealed record SupplierPaymentDto(Guid Id, Guid SupplierId, decimal Amount);

    internal sealed record ClosingDto(
        decimal OpeningCash,
        decimal CashSales,
        decimal CardSales,
        decimal CreditSales,
        decimal Expenses,
        decimal ExpectedCash,
        decimal ActualCash,
        decimal Difference);

    internal sealed record ActivityDto(Guid Id, Guid? EmployeeId, string Action, string Detail);

    internal sealed record SettingsDto(
        string StoreName,
        string? OwnerName,
        string WhatsappTemplate,
        string Currency,
        int DefaultMinStock,
        string Language);

    internal sealed record DashboardDto(
        int ProductCount,
        int LowStockCount,
        int OutOfStockCount,
        decimal StockCostValue,
        decimal StockRetailValue,
        decimal TodaySales,
        decimal TodayProfit,
        int UnknownProfitSalesCount,
        decimal UnknownProfitAmount,
        decimal TodayExpenses,
        int TodaySalesCount,
        decimal TotalCustomerDebt,
        decimal TotalSupplierDebt,
        decimal ExpectedCash,
        FrozenProductsDto FrozenProducts,
        List<TopProductDto> TopProducts,
        List<LowStockProductDto> LowStock,
        List<DailyPointDto> DailySeries,
        List<MonthlyPointDto> MonthlySeries,
        List<RecentSaleDto> RecentSales,
        List<RecentPaymentDto> RecentPayments);

    internal sealed record FrozenProductsDto(int Days30, int Days60, int Days90, List<FrozenProductDto> Items);

    internal sealed record FrozenProductDto(Guid Id, string Name, int Quantity, decimal FrozenValue, int? DaysSinceLastSale);

    internal sealed record TopProductDto(Guid ProductId, string Name, int QuantitySold, decimal Revenue);

    internal sealed record LowStockProductDto(Guid ProductId, string Name, int Quantity, int MinStock);

    internal sealed record DailyPointDto(DateOnly Date, decimal Sales, decimal Profit);

    internal sealed record MonthlyPointDto(string Month, decimal Profit);

    internal sealed record RecentSaleDto(Guid Id, DateOnly Date, string ProductName, int Quantity, decimal TotalAmount, string PaymentType, string? CustomerName);

    internal sealed record RecentPaymentDto(Guid Id, DateOnly Date, string CustomerName, decimal Amount);

    internal sealed record SummaryDto(
        string Period,
        DateOnly? From,
        DateOnly? To,
        decimal SalesTotal,
        decimal Profit,
        decimal Expenses,
        int SalesCount,
        decimal NetProfit,
        decimal CashSales,
        decimal CardSales,
        decimal CreditSales,
        int UnknownProfitSalesCount,
        decimal UnknownProfitAmount);

    internal sealed record ErrorDto(string Code, string Message);
}
