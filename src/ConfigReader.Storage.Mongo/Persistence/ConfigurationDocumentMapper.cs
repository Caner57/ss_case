using ConfigReader.Core.Domain;

namespace ConfigReader.Storage.Mongo.Persistence;

/// <summary>Maps between the domain <see cref="ConfigurationRecord"/> and the persistence
/// <see cref="ConfigurationDocument"/>.</summary>
internal static class ConfigurationDocumentMapper
{
    public static ConfigurationRecord ToRecord(ConfigurationDocument document) => new()
    {
        Id = document.Id ?? string.Empty,
        Name = document.Name,
        Type = document.Type,
        Value = document.Value,
        IsActive = document.IsActive,
        ApplicationName = document.ApplicationName
    };

    public static ConfigurationDocument ToDocument(ConfigurationRecord record) => new()
    {
        // An empty Id means "new record"; let MongoDB generate the ObjectId on insert.
        Id = string.IsNullOrWhiteSpace(record.Id) ? null : record.Id,
        Name = record.Name,
        Type = record.Type,
        Value = record.Value,
        IsActive = record.IsActive,
        ApplicationName = record.ApplicationName
    };
}
