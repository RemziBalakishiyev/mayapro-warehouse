using System.Data.Common;
using MayaPro.WarehouseApi.SharedKernel.Application;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace MayaPro.WarehouseApi.SharedKernel.Infrastructure;

/// <summary>
/// Scoped <see cref="IDbConnectionFactory"/>: owns exactly one <see cref="SqlConnection"/> for the scope
/// and hands it to every participating DbContext, so they can share a transaction. The connection is
/// created lazily and disposed when the scope ends.
/// </summary>
public sealed class SqlConnectionFactory : IDbConnectionFactory, IAsyncDisposable, IDisposable
{
    private readonly string _connectionString;
    private SqlConnection? _connection;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
                            ?? throw new InvalidOperationException("ConnectionStrings:Default konfiqurasiyada yoxdur");
    }

    public DbConnection GetConnection() => _connection ??= new SqlConnection(_connectionString);

    public void Dispose() => _connection?.Dispose();

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
