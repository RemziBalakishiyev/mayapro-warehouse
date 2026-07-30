using System.Text;
using MayaPro.WarehouseApi.Modules.Exports.Application;
using MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportSaleInvoicePdf;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Exports.Tests;

/// <summary>
/// Unit tests for the sale invoice PDF's qismən ödənişli satış line (BE#15 / AC10 / TC11): "Ödənildi: X ₼ ·
/// Qalıq borc: Y ₼" appears on a partially paid Nisyə sale and is omitted once nothing remains. QuestPDF
/// output is only asserted at the magic-bytes/byte-diff level (same convention as
/// <see cref="ExportProductLabelsPdfHandlerTests"/>) — the solution has no PDF text-extraction dependency.
/// </summary>
public sealed class ExportSaleInvoicePdfHandlerTests
{
    private static readonly Guid SaleId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 7, 30);
    private static readonly StoreInfo Store = new("Test Mağaza", "Bakı", "0501112233", "AZN");
    private static readonly CustomerInfo Customer = new("Qismən ödəyən müştəri", "0559998877", Debt: 200m);

    [Fact]
    public async Task TC11_Partially_Paid_Credit_Sale_Produces_A_Pdf()
    {
        // TC1's shape: Total=500, paid 300 → remaining 200.
        SaleInvoiceInfo sale = Sale(totalAmount: 500m, paidAmount: 300m);

        Result<ExportFileResult> result = await Handler(sale).Handle(SaleId, default);

        Assert.True(result.IsSuccess);
        AssertIsPdf(result.Value);
    }

    [Fact]
    public async Task Partially_Paid_Invoice_Differs_From_An_Otherwise_Identical_Fully_Paid_One()
    {
        // AC10: the paid/remaining line is only rendered when a balance remains — proven here by the two
        // documents (same sale, same customer, only the paid amount differs) coming out non-identical, and
        // the partially paid one strictly bigger (it carries one extra printed line).
        SaleInvoiceInfo partiallyPaid = Sale(totalAmount: 500m, paidAmount: 300m);
        SaleInvoiceInfo fullyPaid = Sale(totalAmount: 500m, paidAmount: 500m);

        Result<ExportFileResult> partial = await Handler(partiallyPaid).Handle(SaleId, default);
        Result<ExportFileResult> full = await Handler(fullyPaid).Handle(SaleId, default);

        Assert.True(partial.IsSuccess);
        Assert.True(full.IsSuccess);
        AssertIsPdf(partial.Value);
        AssertIsPdf(full.Value);
        Assert.NotEqual(partial.Value.Content, full.Value.Content);
        Assert.True(
            partial.Value.Content.Length > full.Value.Content.Length,
            "the partially paid invoice should be strictly bigger — it carries the extra Ödənildi/Qalıq borc line");
    }

    [Fact]
    public async Task Fully_Paid_Cash_Sale_Still_Produces_A_Valid_Pdf()
    {
        SaleInvoiceInfo sale = new(
            SaleId, Today.ToDateTime(TimeOnly.MinValue), "Nağd mal", "Kateqoriya", Quantity: 1,
            UnitPrice: 100m, Subtotal: 100m, TotalAmount: 100m, PaymentType: WireFormat.PaymentTypes.Cash,
            CustomerId: null, PaidAmount: 100m);

        Result<ExportFileResult> result = await Handler(sale, customer: null).Handle(SaleId, default);

        Assert.True(result.IsSuccess);
        AssertIsPdf(result.Value);
    }

    private static SaleInvoiceInfo Sale(decimal totalAmount, decimal paidAmount) =>
        new(
            SaleId,
            Today.ToDateTime(TimeOnly.MinValue),
            "Qismən ödənişli mal",
            "Kateqoriya",
            Quantity: 1,
            UnitPrice: totalAmount,
            Subtotal: totalAmount,
            TotalAmount: totalAmount,
            PaymentType: WireFormat.PaymentTypes.Credit,
            CustomerId: CustomerId,
            PaidAmount: paidAmount);

    private static void AssertIsPdf(ExportFileResult file)
    {
        Assert.NotEmpty(file.Content);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(file.Content, 0, 4));
    }

    private static ExportSaleInvoicePdfHandler Handler(SaleInvoiceInfo sale, CustomerInfo? customer = null) =>
        new(
            new StubSalesModule(SaleId, sale),
            new StubCustomersModule(customer ?? Customer),
            new StubSettingsModule(Store),
            new FixedDateProvider(Today));
}
