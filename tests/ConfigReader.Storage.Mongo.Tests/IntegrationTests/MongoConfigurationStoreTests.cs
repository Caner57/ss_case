using ConfigReader.Core.Application;
using ConfigReader.Core.Domain;
using ConfigReader.Storage.Mongo;
using FluentAssertions;
using MongoDB.Driver;

namespace ConfigReader.Storage.Mongo.Tests.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="MongoConfigurationStore"/> against a real MongoDB
/// container (Testcontainers). Verifies tenant isolation, active-only filtering, CRUD
/// round-trips and that typed filter builders neutralise NoSQL-operator injection.
/// </summary>
[Collection(MongoTestCollection.Name)]
public sealed class MongoConfigurationStoreTests
{
    private const string ServiceA = "SERVICE-A";
    private const string ServiceB = "SERVICE-B";

    private readonly IMongoDatabase _database;

    public MongoConfigurationStoreTests(MongoContainerFixture fixture)
    {
        var client = new MongoClient(fixture.ConnectionString);
        _database = client.GetDatabase("configdb_test");
    }

    private (MongoConfigurationStore store, IConfigurationWriter writer) NewStore()
    {
        var options = new MongoStorageOptions
        {
            CollectionName = "cfg_" + Guid.NewGuid().ToString("N")
        };
        var store = new MongoConfigurationStore(_database, options);
        return (store, store);
    }

    private static ConfigurationRecord Record(
        string name, string type, string value, bool isActive, string applicationName) => new()
    {
        Id = string.Empty,
        Name = name,
        Type = type,
        Value = value,
        IsActive = isActive,
        ApplicationName = applicationName
    };

    [Fact]
    public async Task GetActiveRecordsAsync_returns_only_records_of_the_requested_application()
    {
        var (store, writer) = NewStore();
        await writer.AddAsync(Record("SiteName", "string", "soty.io", isActive: true, ServiceA));
        await writer.AddAsync(Record("IsBasketEnabled", "bool", "true", isActive: true, ServiceB));

        var results = await store.GetActiveRecordsAsync(ServiceA);

        results.Should().ContainSingle();
        results.Should().OnlyContain(record => record.ApplicationName == ServiceA);
        results.Should().NotContain(record => record.ApplicationName == ServiceB);
    }

    [Fact]
    public async Task GetActiveRecordsAsync_never_returns_inactive_records()
    {
        var (store, writer) = NewStore();
        await writer.AddAsync(Record("SiteName", "string", "soty.io", isActive: true, ServiceA));
        await writer.AddAsync(Record("MaxItemCount", "int", "50", isActive: false, ServiceA));

        var results = await store.GetActiveRecordsAsync(ServiceA);

        results.Should().ContainSingle(record => record.Name == "SiteName");
        results.Should().NotContain(record => record.Name == "MaxItemCount");
    }

    [Fact]
    public async Task AddAsync_makes_the_new_record_visible_for_its_application()
    {
        var (store, writer) = NewStore();

        await writer.AddAsync(Record("SiteName", "string", "soty.io", isActive: true, ServiceA));

        var results = await store.GetActiveRecordsAsync(ServiceA);
        results.Should().ContainSingle(record => record.Name == "SiteName" && record.Value == "soty.io");
    }

    [Fact]
    public async Task UpdateAsync_persists_the_new_value_for_a_single_record()
    {
        var (store, writer) = NewStore();
        await writer.AddAsync(Record("SiteName", "string", "soty.io", isActive: true, ServiceA));
        var stored = (await store.GetActiveRecordsAsync(ServiceA)).Single();

        var changed = new ConfigurationRecord
        {
            Id = stored.Id,
            Name = stored.Name,
            Type = stored.Type,
            Value = "updated.io",
            IsActive = stored.IsActive,
            ApplicationName = stored.ApplicationName
        };
        await writer.UpdateAsync(changed);

        var results = await store.GetActiveRecordsAsync(ServiceA);
        results.Should().ContainSingle(record => record.Value == "updated.io");
    }

    [Fact]
    public async Task GetActiveRecordsAsync_treats_mongo_operator_string_as_a_literal_not_an_operator()
    {
        var (store, writer) = NewStore();
        await writer.AddAsync(Record("SiteName", "string", "soty.io", isActive: true, ServiceA));
        await writer.AddAsync(Record("IsBasketEnabled", "bool", "true", isActive: true, ServiceB));

        // A typed filter builder must match this as plain text, not interpret it as a
        // {"$ne": null} operator that would leak every record (NoSQL injection attempt).
        var injection = "{\"$ne\": null}";
        var results = await store.GetActiveRecordsAsync(injection);

        results.Should().BeEmpty();
    }
}
