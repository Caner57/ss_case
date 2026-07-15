using Testcontainers.Redis;

namespace ConfigReader.Broker.Redis.Tests.IntegrationTests;

/// <summary>
/// Spins up a throwaway Redis container so the pub/sub adapters can be exercised against a real
/// broker. Shared across the broker integration tests via <see cref="RedisCollection"/>.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(RedisCollection.Name)]
public sealed class RedisCollection : ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "redis";
}
