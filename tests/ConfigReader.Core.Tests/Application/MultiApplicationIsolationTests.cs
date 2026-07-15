using ConfigReader.Core.Application;
using ConfigReader.Core.Domain;
using ConfigReader.Core.Tests.Fakes;
using FluentAssertions;

namespace ConfigReader.Core.Tests.Application;

/// <summary>
/// Library-level (in-process) isolation guarantees between two readers with different
/// ApplicationNames. The end-to-end proof against a real server-side query lives in
/// <c>IntegrationTests/MultiApplicationIsolationIntegrationTests</c>.
/// </summary>
public sealed class MultiApplicationIsolationTests
{
    private const string ServiceA = "SERVICE-A";
    private const string ServiceB = "SERVICE-B";

    private static ConfigurationRecord Record(
        string name, string type, string value, string applicationName) => new()
    {
        Id = name + applicationName,
        Name = name,
        Type = type,
        Value = value,
        IsActive = true,
        ApplicationName = applicationName
    };

    private static FakeConfigurationStore SharedStore() => new(
        Record("SiteName", "string", "soty.io", ServiceA),
        Record("IsBasketEnabled", "bool", "1", ServiceB));

    [Fact]
    public void Each_reader_sees_only_its_own_application_records()
    {
        var store = SharedStore();
        using var readerA = new ConfigurationReader(ServiceA, store, refreshTimerIntervalInMs: 1000);
        using var readerB = new ConfigurationReader(ServiceB, store, refreshTimerIntervalInMs: 1000);

        readerA.GetValue<string>("SiteName").Should().Be("soty.io");
        readerB.GetValue<bool>("IsBasketEnabled").Should().BeTrue();
    }

    [Fact]
    public void Reader_a_cannot_read_a_key_belonging_to_service_b()
    {
        var store = SharedStore();
        using var readerA = new ConfigurationReader(ServiceA, store, refreshTimerIntervalInMs: 1000);

        var act = () => readerA.GetValue<bool>("IsBasketEnabled");

        act.Should().Throw<ConfigurationKeyNotFoundException>();
    }

    [Fact]
    public void Reader_b_cannot_read_a_key_belonging_to_service_a()
    {
        var store = SharedStore();
        using var readerB = new ConfigurationReader(ServiceB, store, refreshTimerIntervalInMs: 1000);

        var act = () => readerB.GetValue<string>("SiteName");

        act.Should().Throw<ConfigurationKeyNotFoundException>();
    }

    [Fact]
    public void Reader_only_ever_queries_the_store_with_its_own_application_name()
    {
        var store = SharedStore();
        using var readerA = new ConfigurationReader(ServiceA, store, refreshTimerIntervalInMs: 1000);

        store.LastRequestedApplicationName.Should().Be(ServiceA);
    }
}
