using System.Data.Common;

namespace MayaPro.WarehouseApi.SharedKernel.Application;

/// <summary>
/// Provides the single database connection shared by every participating module DbContext within one
/// DI scope (one request). Sharing one connection is what lets modules with separate DbContexts enlist
/// in a single transaction — the modular-monolith answer to distributed transactions.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>The scope's shared connection. Created lazily; the same instance every call in the scope.</summary>
    DbConnection GetConnection();
}
