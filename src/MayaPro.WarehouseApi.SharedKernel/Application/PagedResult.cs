namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// A page of items plus the total count, for skip/take style pagination.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Skip, int Take)
{
    public static PagedResult<T> Empty(int skip, int take) =>
        new(Array.Empty<T>(), 0, skip, take);
}
