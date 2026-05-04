namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Shares the integration database fixture across tests that mutate seeded data.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestDatabaseFixture>
{
    /// <summary>
    /// xUnit collection name for tests using the shared integration database.
    /// </summary>
    public const string Name = "integration-test-database";
}
