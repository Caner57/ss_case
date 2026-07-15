using ConfigReader.Storage.Mongo.Persistence;
using MongoDB.Driver;

namespace ConfigReader.Storage.Mongo;

/// <summary>
/// Creates the indexes documented in SCHEMA.md (CFG-2.1) on the configurations
/// collection. Index creation is idempotent, so this can run safely on every startup.
/// </summary>
public sealed class MongoConfigurationIndexInitializer
{
    private static readonly IndexKeysDefinitionBuilder<ConfigurationDocument> Keys =
        Builders<ConfigurationDocument>.IndexKeys;

    private readonly IMongoCollection<ConfigurationDocument> _collection;

    public MongoConfigurationIndexInitializer(IMongoDatabase database, MongoStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _collection = database.GetCollection<ConfigurationDocument>(options.CollectionName);
    }

    public MongoConfigurationIndexInitializer(IMongoDatabase database)
        : this(database, new MongoStorageOptions())
    {
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        // Primary access pattern: (applicationName, isActive) — the library's only query.
        var libraryQueryIndex = new CreateIndexModel<ConfigurationDocument>(
            Keys.Ascending(document => document.ApplicationName)
                .Ascending(document => document.IsActive));

        // One definition per key within an application; the same Name may exist under a
        // different application, so uniqueness is scoped to (applicationName, name).
        var uniqueKeyPerApplication = new CreateIndexModel<ConfigurationDocument>(
            Keys.Ascending(document => document.ApplicationName)
                .Ascending(document => document.Name),
            new CreateIndexOptions { Unique = true });

        return _collection.Indexes.CreateManyAsync(
            new[] { libraryQueryIndex, uniqueKeyPerApplication },
            cancellationToken);
    }
}
