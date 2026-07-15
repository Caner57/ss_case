using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ConfigReader.Admin.Api.Infrastructure;

/// <summary>
/// MongoDB persistence model for an audit entry, kept separate from the
/// <see cref="Application.AuditEntry"/> domain record. Lives in its own collection so the
/// audit trail is physically distinct from the configuration data it describes.
/// </summary>
internal sealed class AuditEntryDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfNull]
    public string? Id { get; set; }

    [BsonElement("timestampUtc")]
    public DateTime TimestampUtc { get; set; }

    [BsonElement("actor")]
    public string Actor { get; set; } = string.Empty;

    [BsonElement("applicationName")]
    public string ApplicationName { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("oldValue")]
    public string? OldValue { get; set; }

    [BsonElement("newValue")]
    public string NewValue { get; set; } = string.Empty;

    [BsonElement("operation")]
    public string Operation { get; set; } = string.Empty;
}
