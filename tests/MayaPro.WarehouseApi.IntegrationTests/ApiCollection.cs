namespace MayaPro.WarehouseApi.IntegrationTests;

/// <summary>
/// Shares a single <see cref="WarehouseApiFactory"/> (and thus one test database reset + host) across
/// every integration test class, and stops those classes running in parallel — they all target the same
/// physical test database, so a shared, sequential fixture avoids conflicting resets.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<WarehouseApiFactory>
{
    public const string Name = "Api";
}
