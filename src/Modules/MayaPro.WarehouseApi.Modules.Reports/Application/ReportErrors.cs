using MayaPro.WarehouseApi.SharedKernel.Application;

namespace MayaPro.WarehouseApi.Modules.Reports.Application;

/// <summary>Business errors for the Reports module. Messages are user-facing (Azerbaijani).</summary>
public static class ReportErrors
{
    // The code does not end in NotFound/Conflict, so the host maps it to 400 Bad Request.
    public static readonly Error InvalidPeriod =
        new("Reports.InvalidPeriod", "Yanlış hesabat dövrü");
}
