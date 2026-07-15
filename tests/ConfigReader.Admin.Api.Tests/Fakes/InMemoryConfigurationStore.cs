using System.Collections.Concurrent;
using ConfigReader.Core.Application;
using ConfigReader.Core.Domain;

namespace ConfigReader.Admin.Api.Tests.Fakes;

/// <summary>
/// In-memory implementation of the management/write ports so the API can be exercised end-to-end
/// via <c>WebApplicationFactory</c> without a real MongoDB. Keeps CRUD and validation tests fast
/// and hermetic; the real MongoDB adapter is covered separately by its own integration tests.
/// </summary>
public sealed class InMemoryConfigurationStore : IConfigurationManagementStore, IConfigurationWriter
{
    private readonly ConcurrentDictionary<string, ConfigurationRecord> _records = new();
    private int _sequence;

    /// <summary>When set, reads throw — used to prove the API never leaks internal error detail.</summary>
    public bool ThrowOnRead { get; set; }

    public Task<IReadOnlyList<ConfigurationRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnRead)
        {
            throw new InvalidOperationException(
                "SECRET-INTERNAL-DETAIL: mongodb://user:password@db:27017 connection failed");
        }

        IReadOnlyList<ConfigurationRecord> snapshot = _records.Values.ToList();
        return Task.FromResult(snapshot);
    }

    public Task<ConfigurationRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _records.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<string> AddAsync(ConfigurationRecord record, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _sequence).ToString();
        _records[id] = WithId(record, id);
        return Task.FromResult(id);
    }

    public Task UpdateAsync(ConfigurationRecord record, CancellationToken cancellationToken = default)
    {
        _records[record.Id] = record;
        return Task.CompletedTask;
    }

    private static ConfigurationRecord WithId(ConfigurationRecord record, string id) => new()
    {
        Id = id,
        Name = record.Name,
        Type = record.Type,
        Value = record.Value,
        IsActive = record.IsActive,
        ApplicationName = record.ApplicationName
    };
}
