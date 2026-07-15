using ConfigReader.Core.Application;
using ConfigReader.Core.Domain;
using ConfigReader.Storage.Mongo.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ConfigReader.Storage.Mongo;

/// <summary>
/// MongoDB adapter implementing the storage ports. Tenant isolation and the active-only
/// rule are enforced server-side via typed filter builders (never string concatenation),
/// so NoSQL-operator values are matched as plain text rather than interpreted.
/// </summary>
public sealed class MongoConfigurationStore : IConfigurationStore, IConfigurationManagementStore, IConfigurationWriter
{
    private static readonly FilterDefinitionBuilder<ConfigurationDocument> Filter =
        Builders<ConfigurationDocument>.Filter;

    private readonly IMongoCollection<ConfigurationDocument> _collection;

    public MongoConfigurationStore(IMongoDatabase database, MongoStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _collection = database.GetCollection<ConfigurationDocument>(options.CollectionName);
    }

    public MongoConfigurationStore(IMongoDatabase database)
        : this(database, new MongoStorageOptions())
    {
    }

    public async Task<IReadOnlyList<ConfigurationRecord>> GetActiveRecordsAsync(
        string applicationName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

        var activeForApplication = Filter.And(
            Filter.Eq(document => document.ApplicationName, applicationName),
            Filter.Eq(document => document.IsActive, true));

        var documents = await _collection
            .Find(activeForApplication)
            .ToListAsync(cancellationToken);

        return documents.Select(ConfigurationDocumentMapper.ToRecord).ToList();
    }

    public async Task<IReadOnlyList<ConfigurationRecord>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var documents = await _collection
            .Find(Filter.Empty)
            .ToListAsync(cancellationToken);

        return documents.Select(ConfigurationDocumentMapper.ToRecord).ToList();
    }

    public async Task<ConfigurationRecord?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // Ids are stored as ObjectId; a malformed id can never match a real document, so we
        // short-circuit instead of letting the driver throw on serialization.
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        var byId = Filter.Eq(document => document.Id, id);
        var document = await _collection.Find(byId).FirstOrDefaultAsync(cancellationToken);

        return document is null ? null : ConfigurationDocumentMapper.ToRecord(document);
    }

    public async Task<string> AddAsync(ConfigurationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = ConfigurationDocumentMapper.ToDocument(record);
        await _collection.InsertOneAsync(document, options: null, cancellationToken);

        return document.Id ?? string.Empty;
    }

    public Task UpdateAsync(ConfigurationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var document = ConfigurationDocumentMapper.ToDocument(record);
        var byId = Filter.Eq(existing => existing.Id, document.Id);

        return _collection.ReplaceOneAsync(byId, document, options: (ReplaceOptions?)null, cancellationToken);
    }
}
