using ConfigReader.Core.Application;
using ConfigReader.Core.Domain;
using ConfigReader.Core.Tests.Fakes;
using FluentAssertions;

namespace ConfigReader.Core.Tests.Application;

public sealed class StorageFallbackTests
{
    private const string ApplicationName = "SERVICE-A";

    private static ConfigurationRecord SiteName(string value = "soty.io") => new()
    {
        Id = "SiteName",
        Name = "SiteName",
        Type = "string",
        Value = value,
        IsActive = true,
        ApplicationName = ApplicationName
    };

    [Fact]
    public async Task Keeps_serving_the_last_good_value_when_storage_becomes_unreachable()
    {
        var store = new FakeConfigurationStore(SiteName());
        await using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        store.ShouldThrow = true;
        await reader.RefreshOnceAsync();

        var act = () => reader.GetValue<string>("SiteName");
        act.Should().NotThrow();
        reader.GetValue<string>("SiteName").Should().Be("soty.io");
    }

    [Fact]
    public async Task Records_the_storage_failure_so_it_is_observable_not_silently_swallowed()
    {
        var store = new FakeConfigurationStore(SiteName());
        Exception? sunkError = null;
        await using var reader = new ConfigurationReader(
            ApplicationName, store, refreshTimerIntervalInMs: 1000, onRefreshError: ex => sunkError = ex);

        store.ShouldThrow = true;
        await reader.RefreshOnceAsync();

        reader.LastRefreshError.Should().NotBeNull();
        reader.RefreshFailureCount.Should().BeGreaterThan(0);
        sunkError.Should().NotBeNull();
    }

    [Fact]
    public async Task Re_synchronises_automatically_once_storage_recovers()
    {
        var store = new FakeConfigurationStore(SiteName("old.io"));
        await using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        store.ShouldThrow = true;
        await reader.RefreshOnceAsync();
        reader.GetValue<string>("SiteName").Should().Be("old.io");

        store.ShouldThrow = false;
        store.SetRecords(SiteName("new.io"));
        await reader.RefreshOnceAsync();

        reader.GetValue<string>("SiteName").Should().Be("new.io");
        reader.LastRefreshError.Should().BeNull("a successful refresh clears the previous error");
    }

    [Fact]
    public async Task Startup_with_no_successful_load_does_not_crash_and_reports_defined_behaviour()
    {
        var store = new FakeConfigurationStore { ShouldThrow = true };

        await using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        reader.HasLoadedSuccessfully.Should().BeFalse();
        var act = () => reader.GetValue<string>("SiteName");
        act.Should().Throw<ConfigurationKeyNotFoundException>();
    }
}
