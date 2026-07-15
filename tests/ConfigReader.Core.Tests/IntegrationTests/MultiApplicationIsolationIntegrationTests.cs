using ConfigReader.Core.Application;
using ConfigReader.Core.Domain;
using ConfigReader.Storage.Mongo;
using FluentAssertions;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace ConfigReader.Core.Tests.IntegrationTests;

/// <summary>
/// End-to-end proof (CFG-3.6) that two <see cref="ConfigurationReader"/> instances built over the
/// real MongoDB adapter, with different ApplicationNames, cannot see each other's records. Isolation
/// rests on the server-side typed query (CFG-2.2) — no client-side "fetch all then filter" path — so
/// even a cache/memory dump could not surface another tenant's data.
/// </summary>
public sealed class MultiApplicationIsolationIntegrationTests : IAsyncLifetime
{
    private const string ServiceA = "SERVICE-A";
    private const string ServiceB = "SERVICE-B";

    private readonly MongoDbContainer _container = new MongoDbBuilder().WithImage("mongo:7").Build();
    private IMongoDatabase _database = null!;
    private MongoStorageOptions _options = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var client = new MongoClient(_container.GetConnectionString());
        _database = client.GetDatabase("configdb_test");
        _options = new MongoStorageOptions { CollectionName = "cfg_" + Guid.NewGuid().ToString("N") };

        var writer = new MongoConfigurationStore(_database, _options);
        await writer.AddAsync(new ConfigurationRecord
        {
            Id = string.Empty, Name = "SiteName", Type = "string", Value = "soty.io",
            IsActive = true, ApplicationName = ServiceA
        });
        await writer.AddAsync(new ConfigurationRecord
        {
            Id = string.Empty, Name = "IsBasketEnabled", Type = "bool", Value = "1",
            IsActive = true, ApplicationName = ServiceB
        });
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private ConfigurationReader NewReader(string applicationName) =>
        new(applicationName, new MongoConfigurationStore(_database, _options), refreshTimerIntervalInMs: 1000);

    [Fact]
    public void Two_readers_in_the_same_process_stay_fully_isolated()
    {
        using var readerA = NewReader(ServiceA);
        using var readerB = NewReader(ServiceB);

        readerA.GetValue<string>("SiteName").Should().Be("soty.io");
        readerB.GetValue<bool>("IsBasketEnabled").Should().BeTrue();

        var leakAToB = () => readerA.GetValue<bool>("IsBasketEnabled");
        var leakBToA = () => readerB.GetValue<string>("SiteName");

        leakAToB.Should().Throw<ConfigurationKeyNotFoundException>();
        leakBToA.Should().Throw<ConfigurationKeyNotFoundException>();
    }
}
