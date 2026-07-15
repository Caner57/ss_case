using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application;

/// <summary>
/// In-memory snapshot of the active records for one application. Implementations
/// must allow concurrent reads while a background refresh atomically swaps the
/// snapshot (see CFG-3.3).
/// </summary>
public interface IConfigurationCache
{
    /// <summary>Returns the record for <paramref name="key"/> or null if absent.</summary>
    ConfigurationRecord? Find(string key);

    /// <summary>Atomically replaces the whole snapshot with a new set of records.</summary>
    void Replace(IReadOnlyCollection<ConfigurationRecord> records);
}
