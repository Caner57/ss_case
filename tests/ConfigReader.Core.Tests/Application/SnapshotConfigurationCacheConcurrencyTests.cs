using ConfigReader.Core.Application.Caching;
using ConfigReader.Core.Domain;
using FluentAssertions;

namespace ConfigReader.Core.Tests.Application;

public sealed class SnapshotConfigurationCacheConcurrencyTests
{
    private const int KeyCount = 12;
    private const int SwapCount = 5000;

    private static ConfigurationRecord[] SnapshotForGeneration(int generation)
    {
        var records = new ConfigurationRecord[KeyCount];
        for (var i = 0; i < KeyCount; i++)
        {
            records[i] = new ConfigurationRecord
            {
                Id = $"K{i}",
                Name = $"K{i}",
                Type = "int",
                Value = generation.ToString(),
                IsActive = true,
                ApplicationName = "SERVICE-A"
            };
        }

        return records;
    }

    [Fact]
    public async Task Concurrent_reads_during_repeated_swaps_never_throw_or_see_a_torn_snapshot()
    {
        var cache = new SnapshotConfigurationCache();
        cache.Replace(SnapshotForGeneration(0));

        var readerFailures = 0;
        var totalReads = 0L;
        using var writerDone = new ManualResetEventSlim(false);

        var writer = Task.Run(() =>
        {
            for (var generation = 1; generation <= SwapCount; generation++)
            {
                cache.Replace(SnapshotForGeneration(generation));
            }

            writerDone.Set();
        });

        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!writerDone.IsSet)
            {
                var snapshot = cache.CurrentSnapshot;

                // Every published snapshot must be whole (all keys) and internally consistent
                // (all keys share one generation) — proof the swap is atomic, never partial.
                if (snapshot.Count != KeyCount)
                {
                    Interlocked.Increment(ref readerFailures);
                    continue;
                }

                var generation = snapshot["K0"].Value;
                foreach (var entry in snapshot.Values)
                {
                    if (entry.Value != generation)
                    {
                        Interlocked.Increment(ref readerFailures);
                        break;
                    }
                }

                Interlocked.Increment(ref totalReads);
            }
        })).ToArray();

        await Task.WhenAll(readers.Append(writer).ToArray());

        readerFailures.Should().Be(0);
        totalReads.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Readers_observe_the_new_value_after_a_swap_completes()
    {
        var cache = new SnapshotConfigurationCache();
        cache.Replace(SnapshotForGeneration(1));

        cache.Find("K0")!.Value.Should().Be("1");

        cache.Replace(SnapshotForGeneration(2));

        cache.Find("K0")!.Value.Should().Be("2");
    }
}
