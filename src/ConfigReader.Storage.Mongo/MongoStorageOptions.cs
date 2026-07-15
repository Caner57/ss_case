namespace ConfigReader.Storage.Mongo;

/// <summary>
/// Names of the MongoDB database and collection backing the configuration store.
/// Defaults match the schema documented in SCHEMA.md (CFG-2.1).
/// </summary>
public sealed class MongoStorageOptions
{
    public const string DefaultDatabaseName = "configdb";
    public const string DefaultCollectionName = "configurations";

    public string DatabaseName { get; init; } = DefaultDatabaseName;

    public string CollectionName { get; init; } = DefaultCollectionName;
}
