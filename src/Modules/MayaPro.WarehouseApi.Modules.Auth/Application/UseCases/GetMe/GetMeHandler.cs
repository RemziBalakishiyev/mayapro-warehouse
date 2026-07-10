using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.GetMe;

/// <summary>Returns the current user's profile, resolved from the JWT <c>sub</c> claim.</summary>
public sealed class GetMeHandler(IAuthDbContext db)
{
    public async Task<Result<UserDto>> Handle(Guid userId, CancellationToken ct)
    {
        User? user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
            return Result.Failure<UserDto>(AuthErrors.UserNotFound);

        return Result.Success(new UserDto(user.Id, user.FullName, user.Phone, user.Role.ToCode()));
    }
}
