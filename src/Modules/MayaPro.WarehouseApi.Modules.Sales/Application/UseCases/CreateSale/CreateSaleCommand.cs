using MayaPro.WarehouseApi.Modules.Sales.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Sales.Application.Contracts;

namespace MayaPro.WarehouseApi.Modules.Sales.Application.UseCases.CreateSale;

/// <summary>
/// Input for creating a sale. <see cref="PaymentType"/> is a frontend code (<c>"Nağd" | "Kart" | "Nisyə"</c>);
/// <see cref="CustomerId"/> is required for credit (Nisyə) sales.
/// <para>
/// A normal sale sets <see cref="ProductId"/> and the item is taken from the catalogue — category is snapshotted
/// from the product (any <see cref="Category"/> on the command is ignored). A free-form ("manual") sale leaves
/// <see cref="ProductId"/> null and supplies <see cref="ProductName"/> (required) by hand;
/// <see cref="CostPerUnit"/> and <see cref="Category"/> are optional. Cost fields are ignored when
/// <see cref="ProductId"/> is set.
/// </para>
/// <para>
/// <see cref="ExpenseItems"/> is optional and only meaningful for a free-form sale: it documents how the
/// seller-supplied <see cref="CostPerUnit"/> was worked out. It is stored as-is and never recomputes the
/// cost/profit; it is ignored for catalogued sales.
/// </para>
/// <para>
/// <see cref="PurchasePricePerUnit"/> is only meaningful for a free-form sale — the seller already knows
/// the buy price separately from the computed <see cref="CostPerUnit"/> — and is stored as-is (never
/// negative; enforced by the validator). Ignored for catalogued sales, whose value is snapshotted from
/// the product instead.
/// </para>
/// <para>
/// <see cref="PaidAmount"/> (BE#15 — qismən ödənişli satış) is optional: omitted, it defaults to the full
/// total on Nağd/Kart and to zero on Nisyə (back-compat). When it leaves a remaining balance
/// (<c>Total − PaidAmount &gt; 0</c>), the sale is stored as Nisyə regardless of <see cref="PaymentType"/>
/// and <see cref="CustomerId"/> becomes mandatory — see <c>SalePaymentPlan</c>. <see cref="PaidVia"/>
/// (<c>"Nağd"|"Kart"</c>, default Nağd) records how that paid portion was actually received. A Nağd/Kart sale
/// paid in full ignores it (the money follows <see cref="PaymentType"/>); a Nisyə request settled in full at
/// sale time is stored under it, since that is the only thing left saying where the money went.
/// </para>
/// </summary>
public sealed record CreateSaleCommand(
    Guid? ProductId,
    int Quantity,
    decimal SalePrice,
    string PaymentType,
    Guid? CustomerId,
    string? Note,
    string? ProductName = null,
    decimal? CostPerUnit = null,
    string? Category = null,
    IReadOnlyList<SaleExpenseItemDto>? ExpenseItems = null,
    decimal? PurchasePricePerUnit = null,
    decimal? PaidAmount = null,
    string? PaidVia = null) : ISaleWriteCommand;
