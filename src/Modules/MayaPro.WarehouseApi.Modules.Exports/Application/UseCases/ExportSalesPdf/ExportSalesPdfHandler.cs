using MayaPro.WarehouseApi.Modules.Exports.Application;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MayaPro.WarehouseApi.Modules.Exports.Application.UseCases.ExportSalesPdf;

/// <summary>
/// Builds the sales-period PDF report from Sales / Expenses / Settings contracts.
/// </summary>
public sealed class ExportSalesPdfHandler(
    ISalesModule sales,
    IExpensesModule expenses,
    ISettingsModule settings,
    IDateProvider dateProvider)
{
    public async Task<Result<ExportFileResult>> Handle(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        DateOnly today = dateProvider.Today;
        DateOnly rangeFrom = from ?? new DateOnly(today.Year, today.Month, 1);
        DateOnly rangeTo = to ?? today;

        if (rangeFrom > rangeTo)
            return Result.Failure<ExportFileResult>(
                new Error("Exports.InvalidRange", "Başlanğıc tarixi bitiş tarixindən böyük ola bilməz"));

        string storeName = await settings.GetStoreNameAsync(ct);
        IReadOnlyList<SalesReportRow> salesRows = await sales.GetSalesAsync(rangeFrom, rangeTo, ct);
        IReadOnlyList<ExpenseReportRow> expenseRows = await expenses.GetExpensesAsync(rangeFrom, rangeTo, ct);

        ExportFonts.EnsureRegistered();

        decimal salesTotal = salesRows.Sum(s => s.TotalAmount);
        int quantityTotal = salesRows.Sum(s => s.Quantity);
        decimal profitKnown = salesRows.Sum(s => s.Profit ?? 0m);
        int unknownProfitCount = salesRows.Count(s => s.Profit is null);
        decimal expensesTotal = expenseRows.Sum(e => e.Amount);
        decimal cash = salesRows.Where(s => s.PaymentType == WireFormat.PaymentTypes.Cash).Sum(s => s.TotalAmount);
        decimal card = salesRows.Where(s => s.PaymentType == WireFormat.PaymentTypes.Card).Sum(s => s.TotalAmount);
        decimal credit = salesRows.Where(s => s.PaymentType == WireFormat.PaymentTypes.Credit).Sum(s => s.TotalAmount);

        var model = new SalesPdfModel(
            StoreName: storeName,
            From: rangeFrom,
            To: rangeTo,
            SalesCount: salesRows.Count,
            QuantityTotal: quantityTotal,
            SalesTotal: salesTotal,
            Profit: profitKnown,
            UnknownProfitCount: unknownProfitCount,
            ExpensesTotal: expensesTotal,
            CashSales: cash,
            CardSales: card,
            CreditSales: credit,
            Rows: salesRows);

        byte[] bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontFamily(ExportFonts.Family).FontSize(9));

                page.Header().Element(c => ComposeHeader(c, model));
                page.Content().Element(c => ComposeContent(c, model));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Səhifə ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        return Result.Success(new ExportFileResult(
            bytes,
            "application/pdf",
            $"satislar-{rangeFrom:yyyy-MM-dd}-{rangeTo:yyyy-MM-dd}.pdf"));
    }

    private static void ComposeHeader(IContainer container, SalesPdfModel model)
    {
        container.Column(col =>
        {
            col.Item().Text(model.StoreName).Bold().FontSize(14);
            col.Item().Text("Satış hesabatı").Bold().FontSize(12);
            col.Item().Text($"Dövr: {model.From:yyyy-MM-dd} – {model.To:yyyy-MM-dd}").FontSize(10);
            col.Item().PaddingBottom(8);
        });
    }

    private static void ComposeContent(IContainer container, SalesPdfModel model)
    {
        container.Column(col =>
        {
            col.Item().Element(c => ComposeSummary(c, model));
            col.Item().PaddingTop(12).Element(c => ComposeTable(c, model));
        });
    }

    private static void ComposeSummary(IContainer container, SalesPdfModel model)
    {
        container.Border(1).BorderColor(Colors.Grey.Medium).Padding(8).Column(col =>
        {
            col.Item().Text("Xülasə").Bold().FontSize(11);
            col.Item().Text($"Satış sayı: {model.SalesCount}");
            col.Item().Text($"Satılan mal ədədi: {model.QuantityTotal}");
            col.Item().Text($"Ümumi satış: {FormatMoney(model.SalesTotal)}");
            col.Item().Text($"Xalis qazanc: {FormatMoney(model.Profit)}");
            if (model.UnknownProfitCount > 0)
                col.Item().Text($"{model.UnknownProfitCount} satışın qazancı naməlum").Italic();
            col.Item().Text($"Dövrün xərcləri: {FormatMoney(model.ExpensesTotal)}");
            col.Item().Text(
                $"Nağd: {FormatMoney(model.CashSales)} · Kart: {FormatMoney(model.CardSales)} · Nisyə: {FormatMoney(model.CreditSales)}");
        });
    }

    private static void ComposeTable(IContainer container, SalesPdfModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.1f); // tarix
                columns.RelativeColumn(2.2f); // mal
                columns.RelativeColumn(0.6f); // say
                columns.RelativeColumn(0.9f); // qiymət
                columns.RelativeColumn(0.9f); // endirim
                columns.RelativeColumn(0.9f); // yekun
                columns.RelativeColumn(0.9f); // ödəniş
                columns.RelativeColumn(0.9f); // qazanc
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Tarix");
                header.Cell().Element(HeaderCell).Text("Mal");
                header.Cell().Element(HeaderCell).AlignRight().Text("Say");
                header.Cell().Element(HeaderCell).AlignRight().Text("Qiymət");
                header.Cell().Element(HeaderCell).AlignRight().Text("Endirim");
                header.Cell().Element(HeaderCell).AlignRight().Text("Yekun");
                header.Cell().Element(HeaderCell).Text("Ödəniş");
                header.Cell().Element(HeaderCell).AlignRight().Text("Qazanc");
            });

            foreach (SalesReportRow row in model.Rows)
            {
                string productLabel = row.IsManual ? $"{row.ProductName} *" : row.ProductName;
                table.Cell().Element(BodyCell).Text(row.Date.ToString("yyyy-MM-dd"));
                table.Cell().Element(BodyCell).Text(productLabel);
                table.Cell().Element(BodyCell).AlignRight().Text(row.Quantity.ToString());
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(row.UnitPrice));
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(row.Discount));
                table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(row.TotalAmount));
                table.Cell().Element(BodyCell).Text(row.PaymentType);
                table.Cell().Element(BodyCell).AlignRight().Text(
                    row.Profit is { } p ? FormatMoney(p) : "—");
            }
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(4).PaddingHorizontal(2)
            .DefaultTextStyle(x => x.Bold().FontSize(8));

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(2)
            .DefaultTextStyle(x => x.FontSize(8));

    private static string FormatMoney(decimal value) => value.ToString("N2");

    private sealed record SalesPdfModel(
        string StoreName,
        DateOnly From,
        DateOnly To,
        int SalesCount,
        int QuantityTotal,
        decimal SalesTotal,
        decimal Profit,
        int UnknownProfitCount,
        decimal ExpensesTotal,
        decimal CashSales,
        decimal CardSales,
        decimal CreditSales,
        IReadOnlyList<SalesReportRow> Rows);
}
