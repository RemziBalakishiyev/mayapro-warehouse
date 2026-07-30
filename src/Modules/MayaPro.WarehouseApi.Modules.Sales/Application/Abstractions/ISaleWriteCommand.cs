namespace MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;

/// <summary>
/// The fields <c>CreateSaleCommand</c> and <c>UpdateSaleCommand</c> have in common — an update is a full
/// reverse-and-reapply, so both carry the same sale values and obey the same rules. Exists so those rules can
/// live once in <see cref="SaleWriteValidator{TCommand}"/> instead of being copied per use case.
/// </summary>
public interface ISaleWriteCommand
{
    Guid? ProductId { get; }

    string? ProductName { get; }

    int Quantity { get; }

    decimal SalePrice { get; }

    /// <summary>Wire payment code (<c>"Nağd" | "Kart" | "Nisyə"</c>).</summary>
    string PaymentType { get; }

    Guid? CustomerId { get; }

    decimal? PurchasePricePerUnit { get; }

    /// <summary>BE#15 — how much was actually received; null falls back to the payment type's default.</summary>
    decimal? PaidAmount { get; }

    /// <summary>BE#15 — wire code of how the paid portion was received (<c>"Nağd" | "Kart"</c>).</summary>
    string? PaidVia { get; }
}
