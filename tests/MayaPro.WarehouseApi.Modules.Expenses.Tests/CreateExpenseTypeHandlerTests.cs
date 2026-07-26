using MayaPro.WarehouseApi.Modules.Expenses.Application.UseCases.CreateExpenseType;
using MayaPro.WarehouseApi.Modules.Expenses.Domain;
using MayaPro.WarehouseApi.Modules.Expenses.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MayaPro.WarehouseApi.Modules.Expenses.Tests;

/// <summary>
/// Unit tests for <see cref="CreateExpenseTypeHandler"/>: the happy path, the empty-name rule, and the
/// duplicate-name rule (which returns <see cref="ExpenseErrors.ExpenseTypeDuplicate"/>). TC-1, TC-2, TC-11.
/// </summary>
public sealed class CreateExpenseTypeHandlerTests
{
    [Fact]
    public async Task Creates_ExpenseType_When_Name_Is_New()
    {
        await using ExpensesDbContext db = NewDb();
        var handler = new CreateExpenseTypeHandler(db, new CreateExpenseTypeValidator());

        var result = await handler.Handle(new CreateExpenseTypeCommand("Sığorta"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sığorta", result.Value.Name);
        Assert.Equal(1, await db.ExpenseTypes.CountAsync());
    }

    [Fact]
    public async Task Trims_Name_Before_Storing()
    {
        await using ExpensesDbContext db = NewDb();
        var handler = new CreateExpenseTypeHandler(db, new CreateExpenseTypeValidator());

        var result = await handler.Handle(new CreateExpenseTypeCommand("  Sığorta  "), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sığorta", result.Value.Name);
    }

    [Fact]
    public async Task Duplicate_Name_Returns_ExpenseTypeDuplicate_Error()
    {
        await using ExpensesDbContext db = NewDb();
        db.ExpenseTypes.Add(ExpenseType.Create("Yol pulu"));
        await db.SaveChangesAsync();

        var handler = new CreateExpenseTypeHandler(db, new CreateExpenseTypeValidator());

        var result = await handler.Handle(new CreateExpenseTypeCommand("Yol pulu"), default);

        Assert.True(result.IsFailure);
        Assert.Equal(ExpenseErrors.ExpenseTypeDuplicate, result.Error);
        Assert.Equal("Bu xərc növü artıq mövcuddur", result.Error.Message);
        Assert.Equal(1, await db.ExpenseTypes.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_Name_Fails_Validation(string name)
    {
        await using ExpensesDbContext db = NewDb();
        var handler = new CreateExpenseTypeHandler(db, new CreateExpenseTypeValidator());

        var result = await handler.Handle(new CreateExpenseTypeCommand(name), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Xərc növü adı boş ola bilməz", result.Error.Message);
        Assert.Equal(0, await db.ExpenseTypes.CountAsync());
    }

    private static ExpensesDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"expenses-tests-{Guid.NewGuid()}")
            .Options;
        return new ExpensesDbContext(options);
    }
}
