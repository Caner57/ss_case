using ConfigReader.Core.Domain;
using ConfigReader.Storage.Mongo.Persistence;
using MongoDB.Driver;

namespace ConfigReader.Storage.Mongo;

/// <summary>
/// Idempotently seeds the configurations collection on first startup: records are
/// inserted only when the collection is empty, so restarting the application never
/// duplicates or overwrites existing data.
/// </summary>
public sealed class MongoConfigurationSeeder
{
    private readonly IMongoCollection<ConfigurationDocument> _collection;

    public MongoConfigurationSeeder(IMongoDatabase database, MongoStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _collection = database.GetCollection<ConfigurationDocument>(options.CollectionName);
    }

    public MongoConfigurationSeeder(IMongoDatabase database)
        : this(database, new MongoStorageOptions())
    {
    }

    /// <summary>
    /// Inserts <paramref name="records"/> only if the collection is currently empty.
    /// Returns the number of records inserted (0 when seeding was skipped).
    /// </summary>
    public async Task<int> SeedAsync(
        IReadOnlyList<ConfigurationRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        var existingCount = await _collection.CountDocumentsAsync(
            Builders<ConfigurationDocument>.Filter.Empty,
            cancellationToken: cancellationToken);

        if (existingCount > 0)
        {
            return 0;
        }

        var documents = records.Select(ConfigurationDocumentMapper.ToDocument).ToList();
        if (documents.Count == 0)
        {
            return 0;
        }

        await _collection.InsertManyAsync(documents, options: null, cancellationToken);
        return documents.Count;
    }
}
