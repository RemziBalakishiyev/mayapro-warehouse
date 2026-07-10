using FluentValidation;
using MayaPro.WarehouseApi.Modules.Auth.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Auth.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Auth.Application.UseCases.Login;

/// <summary>
/// Authenticates by phone + password: finds the user, checks it is active, verifies the BCrypt hash,
/// then issues a JWT. Failures return Azerbaijani business errors — the same "invalid credentials"
/// message for both unknown phone and wrong password, so we don't leak which phones exist.
/// </summary>
public sealed class LoginHandler(
    IAuthDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IValidator<LoginCommand> validator)
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<LoginResponse>(
                Error.Validation(validation.Errors[0].ErrorMessage));

        User? user = await db.Users
            .FirstOrDefaultAsync(u => u.Phone == command.Phone, ct);

        if (user is null)
            return Result.Failure<LoginResponse>(AuthErrors.InvalidCredentials);

        if (!user.IsActive)
            return Result.Failure<LoginResponse>(AuthErrors.UserInactive);

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
            return Result.Failure<LoginResponse>(AuthErrors.InvalidCredentials);

        string token = tokenService.CreateToken(user);
        var dto = new UserDto(user.Id, user.FullName, user.Phone, user.Role.ToCode());

        return Result.Success(new LoginResponse(token, dto));
    }
}
