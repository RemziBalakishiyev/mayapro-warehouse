using FluentValidation;
using MayaPro.WarehouseApi.Modules.Products.Application.Abstractions;
using MayaPro.WarehouseApi.Modules.Products.Application.Contracts;
using MayaPro.WarehouseApi.Modules.Products.Domain;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Products.Application.UseCases.CreateCategory;

/// <summary>
/// Creates a managed category. Rejects an empty name (validator) or a duplicate one
/// (<see cref="ProductErrors.CategoryDuplicate"/>). The comparison is case-insensitive, matching the
/// unique index under SQL Server's default collation.
/// </summary>
public sealed class CreateCategoryHandler(
    IProductsDbContext db,
    IValidator<CreateCategoryCommand> validator)
{
    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand command, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return Result.Failure<CategoryDto>(Error.Validation(validation.Errors[0].ErrorMessage));

        string name = command.Name.Trim();

        bool exists = await db.Categories.AnyAsync(c => c.Name == name, ct);
        if (exists)
            return Result.Failure<CategoryDto>(ProductErrors.CategoryDuplicate);

        var category = Category.Create(name);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return Result.Success(new CategoryDto(category.Id, category.Name));
    }
}
