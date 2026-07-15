using ConfigReader.Core.Application;
using MongoDB.Driver;

namespace ConfigReader.Storage.Mongo;

/// <summary>
/// Concrete <see cref="IConfigurationStoreFactory"/> for MongoDB. Turns the raw connection string
/// handed to <c>new ConfigurationReader(applicationName, connectionString, refreshTimerIntervalInMs)</c>
/// into a live <see cref="MongoConfigurationStore"/>, so the case's three-parameter contract is
/// honoured without <see cref="ConfigReader.Core"/> ever referencing MongoDB. A consuming host
/// registers it once at its composition root via
/// <see cref="ConfigurationReader.UseStoreFactory"/>; dependency direction stays
/// Infrastructure -> Core.
/// <para>
/// The target database name is taken from the connection string when present (e.g.
/// <c>.../configdb?authSource=configdb</c>); otherwise it falls back to
/// <see cref="MongoStorageOptions.DatabaseName"/>. The <see cref="MongoClient"/> connects lazily, so
/// constructing the store never blocks and a temporarily unreachable server is handled by the
/// reader's fallback/refresh loop rather than surfacing here.
/// </para>
/// </summary>
public sealed class MongoConfigurationStoreFactory : IConfigurationStoreFactory
{
    private readonly MongoStorageOptions _options;

    public MongoConfigurationStoreFactory()
        : this(new MongoStorageOptions())
    {
    }

    public MongoConfigurationStoreFactory(MongoStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public IConfigurationStore Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var url = MongoUrl.Create(connectionString);
        var client = new MongoClient(url);
        var database = client.GetDatabase(ResolveDatabaseName(url));

        return new MongoConfigurationStore(database, _options);
    }

    private string ResolveDatabaseName(MongoUrl url) =>
        string.IsNullOrWhiteSpace(url.DatabaseName) ? _options.DatabaseName : url.DatabaseName;
}
