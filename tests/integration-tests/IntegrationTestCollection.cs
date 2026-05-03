namespace SekaiPlatform.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestDatabaseFixture>
{
    public const string Name = "integration-test-database";
}
