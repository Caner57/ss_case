using ConfigReader.Core.Application;
using ConfigReader.Core.Domain;
using ConfigReader.Core.Tests.Fakes;
using FluentAssertions;

namespace ConfigReader.Core.Tests.Application;

public sealed class ConfigurationReaderConstructorTests
{
    private const string ApplicationName = "SERVICE-A";

    private static ConfigurationRecord SiteName() => new()
    {
        Id = "1",
        Name = "SiteName",
        Type = "string",
        Value = "soty.io",
        IsActive = true,
        ApplicationName = ApplicationName
    };

    [Fact]
    public void Reads_value_from_store_after_synchronous_initial_load()
    {
        var store = new FakeConfigurationStore(SiteName());

        using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        reader.GetValue<string>("SiteName").Should().Be("soty.io");
    }

    [Fact]
    public void Requests_only_its_own_application_from_the_store()
    {
        var store = new FakeConfigurationStore(SiteName());

        using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        store.LastRequestedApplicationName.Should().Be(ApplicationName);
    }

    [Fact]
    public void Missing_key_throws_defined_not_found_exception_instead_of_crashing()
    {
        var store = new FakeConfigurationStore(SiteName());

        using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);

        var act = () => reader.GetValue<string>("DoesNotExist");

        act.Should().Throw<ConfigurationKeyNotFoundException>();
    }

    [Fact]
    public void Does_not_throw_from_constructor_when_first_load_finds_no_data()
    {
        var store = new FakeConfigurationStore();

        var act = () =>
        {
            using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Does_not_throw_from_constructor_when_storage_is_unreachable_at_startup()
    {
        var store = new FakeConfigurationStore { ShouldThrow = true };

        var act = () =>
        {
            using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1000);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Raises_refresh_interval_below_the_lower_bound_up_to_the_minimum()
    {
        var store = new FakeConfigurationStore(SiteName());

        using var reader = new ConfigurationReader(ApplicationName, store, refreshTimerIntervalInMs: 1);

        reader.RefreshIntervalInMs.Should().Be(ConfigurationReader.MinimumRefreshIntervalInMs);
    }

    [Fact]
    public void Public_constructor_without_a_registered_store_factory_fails_fast_with_a_clear_message()
    {
        ConfigurationReader.ResetStoreFactory();

        var act = () => new ConfigurationReader(ApplicationName, "mongodb://localhost:27017", refreshTimerIntervalInMs: 1000);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*UseStoreFactory*");
    }
}
