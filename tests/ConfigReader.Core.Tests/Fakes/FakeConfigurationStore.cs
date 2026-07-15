using ConfigReader.Core.Application;
using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IConfigurationStore"/> test double. Mimics the real adapter's
/// server-side scoping (only active records for the requested application are returned),
/// and can be told to fail so fallback/refresh behaviour can be exercised deterministically.
/// </summary>
public sealed class FakeConfigurationStore : IConfigurationStore
{
    private volatile IReadOnlyList<ConfigurationRecord> _records;
    private int _callCount;

    public FakeConfigurationStore(params ConfigurationRecord[] records)
    {
        _records = records;
    }

    public bool ShouldThrow { get; set; }

    public int CallCount => Volatile.Read(ref _callCount);

    public string? LastRequestedApplicationName { get; private set; }

    public void SetRecords(params ConfigurationRecord[] records) => _records = records;

    public Task<IReadOnlyList<ConfigurationRecord>> GetActiveRecordsAsync(
        string applicationName,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);
        LastRequestedApplicationName = applicationName;

        if (ShouldThrow)
        {
            throw new InvalidOperationException("Simulated storage outage.");
        }

        IReadOnlyList<ConfigurationRecord> scoped = _records
            .Where(record => record.IsActive && record.ApplicationName == applicationName)
            .ToList();

        return Task.FromResult(scoped);
    }
}
