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
        this HttpClient client, string barcode, int quantity, decimal salePrice = 10m, string supplierId = "sup_1",
        decimal purchasePrice = 5m, IEnumerable<(string Name, decimal Amount)>? expenses = null)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/products",
            ProductBody(barcode, quantity, salePrice, supplierId, purchasePrice, expenses));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    /// <summary>Edits a product in place; only the fields a test cares about differ from the create body.</summary>
    public static async Task<HttpResponseMessage> UpdateProductAsync(
        this HttpClient client, Guid id, string barcode, int quantity, decimal salePrice = 10m,
        string supplierId = "sup_1", decimal purchasePrice = 5m,
        IEnumerable<(string Name, decimal Amount)>? expenses = null)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/products/{id}",
            ProductBody(barcode, quantity, salePrice, supplierId, purchasePrice, expenses));
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static object ProductBody(
        string barcode, int quantity, decimal salePrice, string supplierId, decimal purchasePrice,
        IEnumerable<(string Name, decimal Amount)>? expenses) => new
    {
        name = "Satış test malı",
        category = "Test",
        attributes = new[] { new { name = "Ölçü", value = "M" }, new { name = "Rəng", value = "Qara" } },
        barcode,
        image = "",
        note = "",
        purchasePrice,
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
        expenses = (expenses ?? Array.Empty<(string, decimal)>())
            .Select(e => new { name = e.Name, amount = e.Amount })
            .ToArray()
    };

    public static async Task<CustomerDto> CreateCustomerAsync(
        this HttpClient client, string name, decimal debt = 0m)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/customers", new { name, phone = (string?)null, note = (string?)null, initialDebt = debt });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerDto>())!;
    }

    public static async Task<HttpResponseMessage> AdjustStockAsync(
        this HttpClient client, Guid productId, int delta, string? note = null) =>
        await client.PostAsJsonAsync($"/api/products/{productId}/adjust-stock", new { delta, note });

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

    internal sealed record CustomerDto(
        Guid Id,
        string Name,
        decimal Debt,
        decimal InitialDebt,
        decimal TotalPurchases,
        int PurchaseCount,
        DateTime? LastPurchaseDate);

    internal sealed record SupplierDto(Guid Id, string Name, decimal Debt, int ItemCount);

    internal sealed record ExpenseDto(Guid Id, string Title, string Category, string Source, decimal Amount, Guid? ProductId);

    internal sealed record SaleDto(
        Guid Id,
        Guid? ProductId,
        int Quantity,
        decimal TotalAmount,
        decimal? CostPerUnit,
        decimal? PurchasePricePerUnit,
        decimal? Profit,
        string PaymentType,
        Guid? CustomerId,
        bool IsManual,
        decimal PaidAmount,
        decimal RemainingAmount,
        string PaidVia);

    /// <summary>Wire shape of <c>GET /api/sales</c> — SharedKernel <c>PagedResult&lt;SaleDto&gt;</c> (items/total/skip/take).</summary>
    internal sealed record PagedSalesDto(List<SaleDto> Items, int Total, int Skip, int Take);

    internal sealed record SaleExpenseItemDto(string Name, decimal Amount);

    /// <summary>Subset of <c>GET /api/sales/{id}</c> detail (deserialised by name).</summary>
    internal sealed record SaleDetailDto(
        Guid Id,
        Guid? ProductId,
        string ProductName,
        string? CurrentProductName,
        int Quantity,
        decimal TotalAmount,
        decimal? CostPerUnit,
        decimal? PurchasePricePerUnit,
        decimal? Profit,
        Guid? CustomerId,
        string? CustomerName,
        bool IsManual,
        List<SaleExpenseItemDto> ExpenseItems,
        decimal PaidAmount,
        decimal RemainingAmount,
        string PaidVia);

    internal sealed record CustomerPaymentDto(Guid Id, Guid CustomerId, decimal Amount);

    internal sealed record CustomerHistoryEntryDto(
        DateTime Date, string Type, decimal Amount, string? Note, Guid? SaleId, string? PaymentType);

    /// <summary>Wire shape of one row of <c>GET /api/customers/open-debts</c> (BE#21).</summary>
    internal sealed record OpenDebtDto(
        Guid CustomerId,
        string CustomerName,
        string? Phone,
        string Source,
        DateTime SourceDate,
        string Description,
        decimal OriginalAmount,
        decimal PaidSoFar,
        decimal Remaining,
        int DaysOld);

    internal sealed record OpenDebtsDto(List<OpenDebtDto> Items, decimal TotalRemaining);

    internal sealed record SupplierPaymentDto(Guid Id, Guid SupplierId, decimal Amount);

    internal sealed record SupplierHistoryEntryDto(DateTime Date, string Type, decimal Amount, string? Note);

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
        string? Address,
        string? Phone,
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

    internal sealed record RecentSaleDto(Guid Id, DateOnly Date, string ProductName, string? Category, int Quantity, decimal TotalAmount, string PaymentType, string? CustomerName);

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
        decimal UnknownProfitAmount,
        decimal GeneralExpenses,
        decimal ProductExpenses);

    internal sealed record ErrorDto(string Code, string Message);

    /// <summary>Wire shape of <c>GET /api/reports/products-kpi</c> (BE#27).</summary>
    internal sealed record ProductsKpiDto(
        int ProductCount,
        int TotalStockUnits,
        decimal TotalCostValue,
        decimal TotalSaleValue,
        decimal PotentialProfit,
        int LowStockCount,
        int SoldUnits,
        int PurchasedUnits);

    /// <summary>Wire shape of <c>GET /api/reports/sales-kpi</c> (BE#27).</summary>
    internal sealed record SalesKpiDto(
        int SalesCount,
        decimal TotalRevenue,
        decimal TotalProfit,
        int UnknownProfitSalesCount,
        decimal UnknownProfitAmount,
        List<PaymentTypeKpiDto> ByPayment,
        decimal AvgSale);

    internal sealed record PaymentTypeKpiDto(string Type, decimal Revenue, decimal Profit);

    /// <summary>Wire shape of <c>GET /api/reports/debts-kpi</c> (BE#27).</summary>
    internal sealed record DebtsKpiDto(
        decimal TotalOutstanding,
        int DebtorCount,
        TopDebtorDto? TopDebtor,
        decimal PeriodNewDebt,
        decimal PeriodCollected,
        int? OldestDebtDays);

    internal sealed record TopDebtorDto(string Name, decimal Amount);
}
