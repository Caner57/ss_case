using Testcontainers.MongoDb;

namespace ConfigReader.Storage.Mongo.Tests.IntegrationTests;

/// <summary>
/// Starts a single MongoDB container shared by all tests in the collection. Each test
/// uses its own freshly-named collection so the shared container stays isolation-safe.
/// </summary>
public sealed class MongoContainerFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder()
        .WithImage("mongo:7")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(MongoTestCollection.Name)]
public sealed class MongoTestCollection : ICollectionFixture<MongoContainerFixture>
{
    public const string Name = "mongo-container";
}
