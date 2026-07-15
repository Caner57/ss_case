using ConfigReader.Core.Domain;
using ConfigReader.Storage.Mongo;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ConfigReader.Storage.Mongo.Tests.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="MongoConfigurationSeeder"/>: first-startup seeding
/// of the three case records and idempotency (no duplication / no overwrite).
/// </summary>
[Collection(MongoTestCollection.Name)]
public sealed class MongoConfigurationSeederTests
{
    private readonly IMongoDatabase _database;

    public MongoConfigurationSeederTests(MongoContainerFixture fixture)
    {
        var client = new MongoClient(fixture.ConnectionString);
        _database = client.GetDatabase("configdb_test");
    }

    private (MongoConfigurationSeeder seeder, MongoConfigurationStore store, MongoStorageOptions options) NewComponents()
    {
        var options = new MongoStorageOptions
        {
            CollectionName = "seed_" + Guid.NewGuid().ToString("N")
        };
        return (new MongoConfigurationSeeder(_database, options), new MongoConfigurationStore(_database, options), options);
    }

    private Task<long> CountAllAsync(MongoStorageOptions options) =>
        _database.GetCollection<BsonDocument>(options.CollectionName)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty);

    [Fact]
    public async Task SeedAsync_on_empty_collection_inserts_the_three_case_records()
    {
        var (seeder, store, options) = NewComponents();

        var inserted = await seeder.SeedAsync(CaseSeedData.Records);

        inserted.Should().Be(3);
        (await CountAllAsync(options)).Should().Be(3);

        var serviceA = await store.GetActiveRecordsAsync("SERVICE-A");
        serviceA.Should().ContainSingle(record =>
            record.Name == "SiteName" && record.Value == "soty.io" && record.Type == "string");
        serviceA.Should().NotContain(record => record.Name == "MaxItemCount");

        var serviceB = await store.GetActiveRecordsAsync("SERVICE-B");
        serviceB.Should().ContainSingle(record => record.Name == "IsBasketEnabled");
    }

    [Fact]
    public async Task SeedAsync_is_idempotent_when_run_twice()
    {
        var (seeder, _, options) = NewComponents();

        await seeder.SeedAsync(CaseSeedData.Records);
        var secondRunInserted = await seeder.SeedAsync(CaseSeedData.Records);

        secondRunInserted.Should().Be(0);
        (await CountAllAsync(options)).Should().Be(3);
    }

    [Fact]
    public async Task SeedAsync_skips_when_collection_already_contains_data()
    {
        var (seeder, store, options) = NewComponents();
        await store.AddAsync(new ConfigurationRecord
        {
            Id = string.Empty,
            Name = "ExistingKey",
            Type = "string",
            Value = "keep-me",
            IsActive = true,
            ApplicationName = "SERVICE-A"
        });

        var inserted = await seeder.SeedAsync(CaseSeedData.Records);

        inserted.Should().Be(0);
        (await CountAllAsync(options)).Should().Be(1);
        var serviceA = await store.GetActiveRecordsAsync("SERVICE-A");
        serviceA.Should().ContainSingle(record => record.Name == "ExistingKey");
    }
}
