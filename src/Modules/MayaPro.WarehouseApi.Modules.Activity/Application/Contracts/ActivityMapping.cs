using MayaPro.WarehouseApi.Modules.Activity.Domain;

namespace MayaPro.WarehouseApi.Modules.Activity.Application.Contracts;

/// <summary>Maps <see cref="ActivityLog"/> to the wire DTO (Type → action, Message → detail).</summary>
public static class ActivityMapping
{
    public static ActivityDto ToDto(this ActivityLog log) =>
        new(log.Id, log.UserId, log.Type, log.Message, log.UserName, log.CreatedAt);
}
