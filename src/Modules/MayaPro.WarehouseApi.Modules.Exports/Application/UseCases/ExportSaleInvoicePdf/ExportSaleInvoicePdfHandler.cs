using System.Globalization;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportSaleInvoicePdf;

/// <summary>
/// Builds one sale's invoice (qaimə-faktura) PDF on an A5 page — practical half-sheet for the market.
/// Store identity comes from Settings, the sale from Sales, and on a credit sale the customer block and
/// the running debt from Customers. The line table is built as a list so a future multi-item basket only
/// changes the data, not the layout.
/// </summary>
public sealed class ExportSaleInvoicePdfHandler(
    ISalesModule sales,
    ICustomersModule customers,
    ISettingsModule settings,
    IDateProvider dateProvider)
{
    private static readonly Error SaleNotFound = new("Exports.SaleNotFound", "Satış tapılmadı");

    public async Task<Result<ExportFileResult>> Handle(Guid saleId, CancellationToken ct)
    {
        SaleInvoiceInfo? sale = await sales.GetInvoiceSaleAsync(saleId, ct);
        if (sale is null)
            return Result.Failure<ExportFileResult>(SaleNotFound);

        StoreInfo store = await settings.GetStoreInfoAsync(ct);

        CustomerInfo? customer = null;
        if (sale.PaymentType == WireFormat.PaymentTypes.Credit && sale.CustomerId is { } customerId)
            customer = await customers.GetCustomerInfoAsync(customerId, ct);

        DateTime issuedAt = dateProvider.ToLocalDateTime(sale.Date);
        string invoiceNumber = BuildInvoiceNumber(sale.Id, issuedAt);

        // BE#15 — qismən ödənişli satış: the remaining balance is this specific sale's, not the customer's
        // running total (already shown separately below when the sale is Nisyə).
        decimal remaining = sale.TotalAmount - sale.PaidAmount;

        var model = new InvoiceModel(
            store,
            invoiceNumber,
            issuedAt,
            sale.PaymentType,
            customer,
            Lines: new[] { new InvoiceLine(sale.ProductName, sale.Category, sale.Quantity, sale.UnitPrice, sale.TotalAmount) },
            Total: sale.TotalAmount,
            PaidAmount: sale.PaidAmount,
            RemainingAmount: remaining);

        ExportFonts.EnsureRegistered();

        byte[] bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily(ExportFonts.Family).FontSize(9));

                page.Header().Element(c => ComposeHeader(c, model));
                page.Content().Element(c => ComposeContent(c, model));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();

        return Result.Success(new ExportFileResult(
            bytes,
            "application/pdf",
            $"faktura-{invoiceNumber}.pdf"));
    }

    /// <summary>"SF-20260719-A3F2B1" — local sale date plus the id's first six hex chars, unique enough per day.</summary>
    private static string BuildInvoiceNumber(Guid saleId, DateTime issuedAt) =>
        $"SF-{issuedAt:yyyyMMdd}-{saleId.ToString("N")[..6].ToUpperInvariant()}";

    private static void ComposeHeader(IContainer container, InvoiceModel model)
    {
        container.BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(model.Store.StoreName).Bold().FontSize(16);
                if (!string.IsNullOrWhiteSpace(model.Store.Address))
                    col.Item().Text(model.Store.Address).FontSize(8).FontColor(Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(model.Store.Phone))
                    col.Item().Text($"Tel: {model.Store.Phone}").FontSize(8).FontColor(Colors.Grey.Darken2);
            });

            row.ConstantItem(150).Column(col =>
            {
                col.Item().AlignRight().Text("QAİMƏ-FAKTURA").Bold().FontSize(12);
                col.Item().AlignRight().Text($"№ {model.InvoiceNumber}").FontSize(9);
                col.Item().AlignRight().Text(model.IssuedAt.ToString("dd.MM.yyyy HH:mm")).FontSize(9);
            });
        });
    }

    private static void ComposeContent(IContainer container, InvoiceModel model)
    {
        container.PaddingTop(10).Column(col =>
        {
            col.Item().Element(c => ComposePartyBlock(c, model));
            col.Item().PaddingTop(10).Element(c => ComposeLinesTable(c, model));
            col.Item().PaddingTop(10).Element(c => ComposeTotals(c, model));
        });
    }

    private static void ComposePartyBlock(IContainer container, InvoiceModel model)
    {
        container.Column(col =>
        {
            if (model.PaymentType == WireFormat.PaymentTypes.Credit)
            {
                // A deleted customer leaves the block label-only rather than failing the export.
                col.Item().Text($"Müştəri: {model.Customer?.Name ?? "—"}").Bold().FontSize(10);
                if (!string.IsNullOrWhiteSpace(model.Customer?.Phone))
                    col.Item().Text($"Tel: {model.Customer.Phone}").FontSize(9);
            }
            else
            {
                string label = model.PaymentType == WireFormat.PaymentTypes.Card ? "Kartla ödəniş" : "Nağd satış";
                col.Item().Text(label).Bold().FontSize(10);
            }
        });
    }

    private static void ComposeLinesTable(IContainer container, InvoiceModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3f);    // mal
                columns.RelativeColumn(0.7f);  // say
                columns.RelativeColumn(1.1f);  // vahid qiymət
                columns.RelativeColumn(1.1f);  // məbləğ
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Mal");
                header.Cell().Element(HeaderCell).AlignRight().Text("Say");
                header.Cell().Element(HeaderCell).AlignRight().Text("Qiymət");
                header.Cell().Element(HeaderCell).AlignRight().Text("Məbləğ");
            });

            foreach (InvoiceLine line in model.Lines)
            {
                table.Cell().Element(BodyCell).Column(col =>
                {
                    col.Item().Text(line.ProductName).FontSize(9);
                    if (!string.IsNullOrWhiteSpace(line.Category))
                        col.Item().Text(line.Category).FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(BodyCell).AlignRight().Text(line.Quantity.ToString());
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(line.UnitPrice));
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(line.Amount));
            }
        });
    }

    private static void ComposeTotals(IContainer container, InvoiceModel model)
    {
        container.AlignRight().Column(col =>
        {
            decimal subtotal = model.Lines.Sum(l => l.Amount);
            col.Item().AlignRight().Text($"Cəm: {FormatMoney(subtotal)} {model.Store.Currency}").FontSize(9);
            col.Item().AlignRight().Text($"YEKUN: {FormatMoney(model.Total)} {model.Store.Currency}").Bold().FontSize(13);

            if (model.PaymentType == WireFormat.PaymentTypes.Credit)
            {
                col.Item().PaddingTop(6).AlignRight().Text("Ödəniş: Nisyə").FontSize(9);

                // BE#15 — qismən ödənişli satış: this sale's own paid/remaining split, shown only when a
                // balance actually remains on it (fully paid Nisyə — an edge case — shows neither line).
                if (model.RemainingAmount > 0)
                    col.Item().AlignRight()
                        .Text($"Ödənildi: {FormatAzMoney(model.PaidAmount)} · Qalıq borc: {FormatAzMoney(model.RemainingAmount)}")
                        .Bold().FontSize(9);

                if (model.Customer is { } customer)
                    col.Item().AlignRight()
                        .Text($"Ümumi qalıq borc: {FormatMoney(customer.Debt)} {model.Store.Currency}")
                        .Bold().FontSize(9);
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("Təşəkkür edirik!").FontSize(10);
            col.Item().AlignCenter().Text("MayaPro sistemi ilə hazırlanıb").FontSize(7).FontColor(Colors.Grey.Medium);
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(4).PaddingHorizontal(2)
            .DefaultTextStyle(x => x.Bold().FontSize(8));

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(2)
            .DefaultTextStyle(x => x.FontSize(9));

    private static string FormatMoney(decimal value) => value.ToString("N2");

    // BE#15 — qismən ödənişli satış: the manat sign, comma decimal separator, fixed regardless of the
    // machine's locale (unlike model.Store.Currency, which is whatever the store configured).
    private static readonly NumberFormatInfo AzMoneyFormat = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = " "
    };

    private static string FormatAzMoney(decimal value) => $"{value.ToString("N2", AzMoneyFormat)} ₼";

    private sealed record InvoiceModel(
        StoreInfo Store,
        string InvoiceNumber,
        DateTime IssuedAt,
        string PaymentType,
        CustomerInfo? Customer,
        IReadOnlyList<InvoiceLine> Lines,
        decimal Total,
        decimal PaidAmount,
        decimal RemainingAmount);

    private sealed record InvoiceLine(
        string ProductName,
        string? Category,
        int Quantity,
        decimal UnitPrice,
        decimal Amount);
}
