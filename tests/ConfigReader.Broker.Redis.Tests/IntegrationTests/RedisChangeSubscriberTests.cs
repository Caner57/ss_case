using ConfigReader.Core.Application.Messaging;
using FluentAssertions;
using StackExchange.Redis;

namespace ConfigReader.Broker.Redis.Tests.IntegrationTests;

/// <summary>
/// Verifies the Redis subscribe adapter (CFG-4.3) against a real broker: it delivers signals from
/// its own application channel, ignores another application's channel and malformed payloads
/// (CFG-9.3), and tears the subscription down on dispose.
/// </summary>
[Collection(RedisCollection.Name)]
public sealed class RedisChangeSubscriberTests
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NonDeliveryWindow = TimeSpan.FromSeconds(1);

    private readonly RedisContainerFixture _redis;

    public RedisChangeSubscriberTests(RedisContainerFixture redis) => _redis = redis;

    [Fact]
    public async Task A_signal_published_on_our_channel_is_delivered_to_the_handler()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(_redis.ConnectionString);
        var received = new TaskCompletionSource<ConfigurationChangeSignal>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscriber = new RedisChangeSubscriber(connection);
        await subscriber.SubscribeAsync("SERVICE-A", (signal, _) =>
        {
            received.TrySetResult(signal);
            return Task.CompletedTask;
        });

        await new RedisChangeNotifier(connection)
            .PublishAsync(new ConfigurationChangeSignal("SERVICE-A", "record-1"));

        var signal = await received.Task.WaitAsync(DeliveryTimeout);
        signal.ApplicationName.Should().Be("SERVICE-A");
        signal.RecordId.Should().Be("record-1");
    }

    [Fact]
    public async Task A_service_a_signal_is_never_delivered_to_a_service_b_subscriber()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(_redis.ConnectionString);
        var received = new TaskCompletionSource<ConfigurationChangeSignal>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscriber = new RedisChangeSubscriber(connection);
        await subscriber.SubscribeAsync("SERVICE-B", (signal, _) =>
        {
            received.TrySetResult(signal);
            return Task.CompletedTask;
        });

        await new RedisChangeNotifier(connection)
            .PublishAsync(new ConfigurationChangeSignal("SERVICE-A", "record-1"));

        var act = async () => await received.Task.WaitAsync(NonDeliveryWindow);
        await act.Should().ThrowAsync<TimeoutException>("SERVICE-B must not receive SERVICE-A signals");
    }

    [Fact]
    public async Task A_malformed_payload_on_our_channel_is_ignored_and_never_invokes_the_handler()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(_redis.ConnectionString);
        var received = new TaskCompletionSource<ConfigurationChangeSignal>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscriber = new RedisChangeSubscriber(connection);
        await subscriber.SubscribeAsync("SERVICE-A", (signal, _) =>
        {
            received.TrySetResult(signal);
            return Task.CompletedTask;
        });

        var channel = RedisChannel.Literal(ConfigurationChangeChannel.ForApplication("SERVICE-A"));
        await connection.GetSubscriber().PublishAsync(channel, "}{ not json at all");

        var act = async () => await received.Task.WaitAsync(NonDeliveryWindow);
        await act.Should().ThrowAsync<TimeoutException>("garbage payloads must be ignored, not surfaced");
    }
}
