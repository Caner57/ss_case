using ConfigReader.Admin.Api.Application;
using MongoDB.Driver;

namespace ConfigReader.Admin.Api.Infrastructure;

/// <summary>
/// MongoDB adapter for <see cref="IAuditLogWriter"/>. Writes to a dedicated collection using
/// insert only: it exposes no update or delete operation, so audit entries are append-only and
/// immutable at the application layer — a bad change can always be traced afterwards.
/// </summary>
public sealed class MongoAuditLogWriter : IAuditLogWriter
{
    public const string DefaultCollectionName = "configuration_audit";

    private readonly IMongoCollection<AuditEntryDocument> _collection;

    public MongoAuditLogWriter(IMongoDatabase database, string collectionName = DefaultCollectionName)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        _collection = database.GetCollection<AuditEntryDocument>(collectionName);
    }

    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var document = new AuditEntryDocument
        {
            TimestampUtc = entry.TimestampUtc.UtcDateTime,
            Actor = entry.Actor,
            ApplicationName = entry.ApplicationName,
            Name = entry.Name,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            Operation = entry.Operation.ToString()
        };

        return _collection.InsertOneAsync(document, options: null, cancellationToken);
    }
}
