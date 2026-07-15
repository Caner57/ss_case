using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ConfigReader.Storage.Mongo.Persistence;

/// <summary>
/// MongoDB persistence model for a configuration entry. Kept separate from the domain
/// <c>ConfigurationRecord</c> so the Core stays free of any MongoDB/BSON dependency
/// (DTO ≠ Domain). Field names match SCHEMA.md (CFG-2.1).
/// </summary>
internal sealed class ConfigurationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfNull]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("value")]
    public string Value { get; set; } = string.Empty;

    [BsonElement("isActive")]
    public bool IsActive { get; set; }

    [BsonElement("applicationName")]
    public string ApplicationName { get; set; } = string.Empty;
}
