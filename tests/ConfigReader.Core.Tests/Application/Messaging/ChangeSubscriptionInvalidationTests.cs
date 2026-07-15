using ConfigReader.Core.Application;
using ConfigReader.Core.Application.Messaging;
using ConfigReader.Core.Domain;
using ConfigReader.Core.Tests.Fakes;
using FluentAssertions;

namespace ConfigReader.Core.Tests.Application.Messaging;

/// <summary>
/// Covers the subscribe/invalidation behaviour (CFG-4.3) and the broker-security guarantees
/// (CFG-9.3) at the <see cref="ConfigurationReader"/> level, using a fake subscriber so the reader's
/// reaction to signals can be driven deterministically without a real broker.
/// </summary>
public sealed class ChangeSubscriptionInvalidationTests
{
    private const string ServiceA = "SERVICE-A";
    private const string ServiceB = "SERVICE-B";

    // Long enough that the background poll never fires during a test — so any observed refresh is
    // attributable to the broker signal, not the timer.
    private const int LongInterval = 60_000;

    private static ConfigurationRecord Record(string value, string application = ServiceA) => new()
    {
        Id = "SiteName",
        Name = "SiteName",
        Type = "string",
        Value = value,
        IsActive = true,
        ApplicationName = application
    };

    [Fact]
    public async Task A_signal_for_our_application_refreshes_immediately_without_waiting_for_the_poll_interval()
    {
        var store = new FakeConfigurationStore(Record("old.io"));
        var subscriber = new FakeChangeSubscriber();
        await using var reader = new ConfigurationReader(
            ServiceA, store, refreshTimerIntervalInMs: LongInterval, changeSubscriber: subscriber);
        reader.GetValue<string>("SiteName").Should().Be("old.io");

        store.SetRecords(Record("new.io"));
        await subscriber.PublishSignalAsync(new ConfigurationChangeSignal(ServiceA, "SiteName"));

        reader.GetValue<string>("SiteName").Should().Be("new.io");
    }

    [Fact]
    public async Task The_refreshed_value_always_comes_from_storage_never_from_the_signal_payload()
    {
        // CFG-9.3 (cache poisoning): the signal only names the application/record. Whatever it claims,
        // the reader re-reads the authoritative value from storage.
        var store = new FakeConfigurationStore(Record("stale.io"));
        var subscriber = new FakeChangeSubscriber();
        await using var reader = new ConfigurationReader(
            ServiceA, store, refreshTimerIntervalInMs: LongInterval, changeSubscriber: subscriber);

        store.SetRecords(Record("authoritative.io"));
        await subscriber.PublishSignalAsync(new ConfigurationChangeSignal(ServiceA, "any-forged-id"));

        reader.GetValue<string>("SiteName").Should().Be("authoritative.io");
    }

    [Fact]
    public async Task The_reader_subscribes_only_to_its_own_application_channel()
    {
        var store = new FakeConfigurationStore(Record("soty.io"));
        var subscriber = new FakeChangeSubscriber();
        await using var reader = new ConfigurationReader(
            ServiceA, store, refreshTimerIntervalInMs: LongInterval, changeSubscriber: subscriber);

        subscriber.SubscribedApplicationName.Should().Be(ServiceA);
    }

    [Fact]
    public async Task A_signal_naming_another_application_never_refreshes_this_reader()
    {
        // CFG-9.3 (channel isolation): even if a SERVICE-B signal somehow reaches this handler,
        // the reader ignores it — no re-read, cache untouched.
        var store = new FakeConfigurationStore(Record("soty.io"));
        var subscriber = new FakeChangeSubscriber();
        await using var reader = new ConfigurationReader(
            ServiceA, store, refreshTimerIntervalInMs: LongInterval, changeSubscriber: subscriber);
        var callsAfterInitialLoad = store.CallCount;

        store.SetRecords(Record("changed.io"));
        await subscriber.PublishSignalAsync(new ConfigurationChangeSignal(ServiceB, "SiteName"));

        store.CallCount.Should().Be(callsAfterInitialLoad, "a foreign-application signal must not trigger a re-read");
        reader.GetValue<string>("SiteName").Should().Be("soty.io");
    }

    [Fact]
    public async Task A_signal_for_an_unknown_application_is_ignored_without_error()
    {
        var store = new FakeConfigurationStore(Record("soty.io"));
        var subscriber = new FakeChangeSubscriber();
        await using var reader = new ConfigurationReader(
            ServiceA, store, refreshTimerIntervalInMs: LongInterval, changeSubscriber: subscriber);

        var act = async () => await subscriber.PublishSignalAsync(new ConfigurationChangeSignal("GHOST-APP", "SiteName"));

        await act.Should().NotThrowAsync();
        reader.GetValue<string>("SiteName").Should().Be("soty.io");
    }

    [Fact]
    public async Task A_broker_that_fails_to_subscribe_never_brings_the_reader_down_it_falls_back_to_polling()
    {
        var store = new FakeConfigurationStore(Record("soty.io"));
        var subscriber = new FakeChangeSubscriber { ThrowOnSubscribe = true };

        var construct = () => new ConfigurationReader(
            ServiceA, store, refreshTimerIntervalInMs: LongInterval, changeSubscriber: subscriber);

        var reader = construct.Should().NotThrow("a broker outage must never fail construction").Subject;
        await using var _ = reader;

        reader.GetValue<string>("SiteName").Should().Be("soty.io", "polling keeps serving values when the broker is down");
        reader.LastBrokerError.Should().NotBeNull("the broker failure is surfaced for observability, not swallowed");
    }

    [Fact]
    public async Task Disposing_the_reader_disposes_the_broker_subscription()
    {
        var store = new FakeConfigurationStore(Record("soty.io"));
        var subscriber = new FakeChangeSubscriber();
        var reader = new ConfigurationReader(
            ServiceA, store, refreshTimerIntervalInMs: LongInterval, changeSubscriber: subscriber);

        await reader.DisposeAsync();

        subscriber.IsDisposed.Should().BeTrue("the reader owns the subscription lifetime");
    }
}
