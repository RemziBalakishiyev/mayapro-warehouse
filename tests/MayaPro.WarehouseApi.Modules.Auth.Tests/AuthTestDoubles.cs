using MayaPro.WarehouseApi.Modules.Auth.Domain;
using MayaPro.WarehouseApi.Modules.Auth.Infrastructure;
using MayaPro.WarehouseApi.SharedKernel.Application;
using MayaPro.WarehouseApi.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MayaPro.WarehouseApi.Modules.Auth.Tests;

/// <summary>
/// Hand-written test doubles for the Auth handlers' collaborators (the solution carries no mocking library
/// on purpose), mirroring the Expenses module's doubles.
/// </summary>
internal sealed class FakeUnitOfWork(AuthDbContext db) : IUnitOfWork
{
    /// <summary>True once a transaction was committed — proves the entry + log were saved together.</summary>
    public bool Committed { get; private set; }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(db, () => Committed = true));

    private sealed class FakeTransaction(AuthDbContext db, Action onCommit) : IUnitOfWorkTransaction
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            db.SaveChangesAsync(cancellationToken);

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            onCommit();
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class FakeActivityLogger : IActivityLogger
{
    public List<(string Type, string Message)> Entries { get; } = [];

    public Task LogAsync(string type, string message, Guid? userId, CancellationToken cancellationToken = default)
    {
        Entries.Add((type, message));
        return Task.CompletedTask;
    }
}

internal sealed class FakeCurrentUser(Guid? userId = null) : ICurrentUser
{
    public Guid? UserId { get; } = userId;

    public string? Name => "Test istifadəçi";

    public string? Role => WireFormat.Roles.Owner;

    public bool IsAuthenticated => UserId is not null;
}

/// <summary>UTC-as-local date provider — enough for handlers that only need "which day/month is this".</summary>
internal sealed class FakeDateProvider(DateTime? utcNow = null) : IDateProvider
{
    private readonly DateTime _utcNow = utcNow ?? new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow => _utcNow;

    public DateOnly Today => DateOnly.FromDateTime(_utcNow);

    public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(utc);

    public DateTime ToLocalDateTime(DateTime utc) => utc;

    public (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateOnly localDate) =>
        (localDate.ToDateTime(TimeOnly.MinValue), localDate.AddDays(1).ToDateTime(TimeOnly.MinValue));
}

/// <summary>Captures the log entries a handler/seeder wrote so tests can assert on warnings without a mocking
/// library, mirroring the Customers module's double of the same name.</summary>
internal sealed class FakeLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}

internal static class AuthTestDb
{
    private static readonly BCryptPasswordHasher Hasher = new();

    public static AuthDbContext New()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-tests-{Guid.NewGuid()}")
            .Options;
        return new AuthDbContext(options);
    }

    /// <summary>Adds an employee with a known salary and returns it.</summary>
    public static async Task<User> AddEmployeeAsync(
        this AuthDbContext db,
        string fullName = "Günel Quliyeva",
        string phone = "0554445566",
        UserRole role = UserRole.Seller,
        decimal monthlySalary = 0m)
    {
        var user = User.Create(fullName, phone, null, Hasher.Hash("demo123"), role);
        user.SetMonthlySalary(monthlySalary);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task AddEntryAsync(
        this AuthDbContext db,
        Guid userId,
        SalaryEntryType type,
        decimal amount,
        string month,
        DateTime? date = null)
    {
        db.SalaryEntries.Add(SalaryEntry.Create(
            userId, type, amount, null, date ?? new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc), month, null));
        await db.SaveChangesAsync();
    }
}
