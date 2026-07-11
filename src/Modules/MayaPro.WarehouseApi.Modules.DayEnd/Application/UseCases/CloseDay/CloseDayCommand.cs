namespace MayaPro.WarehouseApi.Modules.DayEnd.Application.UseCases.CloseDay;

/// <summary>
/// Input for closing the day. The client sends only the cash figures and a note; the sales/expense
/// totals for the day are computed server-side.
/// </summary>
public sealed record CloseDayCommand(decimal OpeningCash, decimal ActualCash, string? Note);
