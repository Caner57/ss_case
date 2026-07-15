using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application.Caching;

/// <summary>
/// Concurrency-safe in-memory cache built on an immutable-snapshot + atomic-swap strategy
/// (CFG-3.3). Reads are lock-free: <see cref="Find"/> captures the current snapshot reference
/// once and queries it, so a background <see cref="Replace"/> never blocks readers and never
/// exposes a half-updated view — a reader sees either the whole old snapshot or the whole new
/// one. The snapshot reference is <c>volatile</c>; reference assignment is atomic on the CLR,
/// which is what makes the swap safe without locks on the hot read path.
/// </summary>
public sealed class SnapshotConfigurationCache : IConfigurationCache
{
    private static readonly IReadOnlyDictionary<string, ConfigurationRecord> EmptySnapshot =
        new Dictionary<string, ConfigurationRecord>(StringComparer.Ordinal);

    private volatile IReadOnlyDictionary<string, ConfigurationRecord> _snapshot = EmptySnapshot;

    public ConfigurationRecord? Find(string key)
    {
        var snapshot = _snapshot;
        return snapshot.TryGetValue(key, out var record) ? record : null;
    }

    /// <summary>Captures the whole current snapshot atomically (single volatile read). Used by
    /// tests to assert that <see cref="Replace"/> never publishes a partially-built view.</summary>
    internal IReadOnlyDictionary<string, ConfigurationRecord> CurrentSnapshot => _snapshot;

    public void Replace(IReadOnlyCollection<ConfigurationRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var next = new Dictionary<string, ConfigurationRecord>(records.Count, StringComparer.Ordinal);
        foreach (var record in records)
        {
            next[record.Name] = record;
        }

        _snapshot = next;
    }
}
