using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application;

/// <summary>
/// Storage-agnostic port for mutating configuration records. Kept separate from
/// <see cref="IConfigurationStore"/> (Interface Segregation): the library depends only
/// on the read port, while the Admin API management use-cases depend on this write port.
/// </summary>
public interface IConfigurationWriter
{
    /// <summary>Inserts a new configuration record and returns its store-generated identifier.</summary>
    Task<string> AddAsync(ConfigurationRecord record, CancellationToken cancellationToken = default);

    /// <summary>Replaces an existing record (matched by its <see cref="ConfigurationRecord.Id"/>).</summary>
    Task UpdateAsync(ConfigurationRecord record, CancellationToken cancellationToken = default);
}
