using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application;

/// <summary>
/// Storage-agnostic port for reading configuration records. Implemented by
/// infrastructure adapters (MongoDB, SQL, file, ...). The store is responsible
/// for returning only active records that belong to the requested application,
/// enforcing tenant isolation at the query level.
/// </summary>
public interface IConfigurationStore
{
    Task<IReadOnlyList<ConfigurationRecord>> GetActiveRecordsAsync(
        string applicationName,
        CancellationToken cancellationToken = default);
}
