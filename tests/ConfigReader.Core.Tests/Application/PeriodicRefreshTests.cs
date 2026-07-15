using ConfigReader.Core.Application;
using ConfigReader.Core.Domain;
using ConfigReader.Core.Tests.Fakes;
using FluentAssertions;

namespace ConfigReader.Core.Tests.Application;

public sealed class PeriodicRefreshTests
{
    private const string ApplicationName = "SERVICE-A";

    private static ConfigurationRecord Record(string name, string value, string type = "string") => new()
    {
        Id = name,
        Name = name,
        Type = type,
        Value = value,
        IsActive = true,
        ApplicationName = ApplicationName
    };

    [Fact]
    public async Task Refresh_picks_up_a_newly_added_record()
    {
        var store = new FakeConfigurationStore(Record("SiteName", "soty.io"));
        await using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        store.SetRecords(Record("SiteName", "soty.io"), Record("NewKey", "added"));
        await reader.RefreshOnceAsync();

        reader.GetValue<string>("NewKey").Should().Be("added");
    }

    [Fact]
    public async Task Refresh_reflects_a_changed_value_of_an_existing_record()
    {
        var store = new FakeConfigurationStore(Record("SiteName", "old.io"));
        await using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        reader.GetValue<string>("SiteName").Should().Be("old.io");

        store.SetRecords(Record("SiteName", "new.io"));
        await reader.RefreshOnceAsync();

        reader.GetValue<string>("SiteName").Should().Be("new.io");
    }

    [Fact]
    public async Task Background_timer_updates_the_cache_without_any_manual_trigger()
    {
        var store = new FakeConfigurationStore(Record("SiteName", "soty.io"));
        await using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        store.SetRecords(Record("SiteName", "soty.io"), Record("Later", "appeared"));

        var appeared = await WaitUntilAsync(() =>
        {
            try
            {
                return reader.GetValue<string>("Later") == "appeared";
            }
            catch (ConfigurationKeyNotFoundException)
            {
                return false;
            }
        }, timeout: TimeSpan.FromSeconds(5));

        appeared.Should().BeTrue("the background PeriodicTimer must refresh the cache on its own");
    }

    [Fact]
    public async Task Disposing_stops_the_background_refresh_loop()
    {
        var store = new FakeConfigurationStore(Record("SiteName", "soty.io"));
        var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        await reader.DisposeAsync();
        var callsAtDispose = store.CallCount;

        await Task.Delay(TimeSpan.FromMilliseconds(1300));

        store.CallCount.Should().Be(callsAtDispose, "no further store calls may happen after disposal");
    }

    [Fact]
    public async Task A_single_failing_refresh_does_not_tear_down_the_loop()
    {
        var store = new FakeConfigurationStore(Record("SiteName", "soty.io"));
        await using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        store.ShouldThrow = true;
        await reader.RefreshOnceAsync();

        store.ShouldThrow = false;
        store.SetRecords(Record("SiteName", "recovered.io"));
        await reader.RefreshOnceAsync();

        reader.GetValue<string>("SiteName").Should().Be("recovered.io");
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return condition();
    }
}
