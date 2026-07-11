using MayaPro.WarehouseApi.Modules.Activity.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Activity.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;

namespace MayaPro.WarehouseApi.Modules.Activity.Application;

/// <summary>
/// The real <see cref="IActivityLogger"/>: adds the entry to the Activity context, snapshotting the
/// caller's name from the JWT. It deliberately does <b>not</b> SaveChanges — the calling use case owns the
/// save/commit (the Activity context is enlisted in the same shared transaction), so the log is written
/// atomically with the operation it describes.
/// </summary>
internal sealed class DbActivityLogger(IActivityDbContext db, ICurrentUser currentUser) : IActivityLogger
{
    public Task LogAsync(string type, string message, Guid? userId, CancellationToken cancellationToken = default)
    {
        var entry = ActivityLog.Create(type, message, userId, currentUser.Name);
        db.ActivityLogs.Add(entry);
        return Task.CompletedTask;
    }
}
