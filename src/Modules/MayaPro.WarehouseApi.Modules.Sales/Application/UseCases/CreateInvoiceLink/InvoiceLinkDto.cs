namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateInvoiceLink;

/// <summary>The shareable public invoice URL, ready to paste into WhatsApp.</summary>
public sealed record InvoiceLinkDto(string Url);
