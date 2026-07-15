using ConfigReader.Core.Application.Messaging;
using FluentAssertions;
using StackExchange.Redis;

namespace ConfigReader.Broker.Redis.Tests.IntegrationTests;

/// <summary>
/// Verifies the Redis publish adapter (CFG-4.2) against a real broker: a signal lands on the
/// application-specific channel and does not leak to another application's channel (CFG-9.3
/// isolation).
/// </summary>
[Collection(RedisCollection.Name)]
public sealed class RedisChangeNotifierTests
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NonDeliveryWindow = TimeSpan.FromSeconds(1);

    private readonly RedisContainerFixture _redis;

    public RedisChangeNotifierTests(RedisContainerFixture redis) => _redis = redis;

    [Fact]
    public async Task PublishAsync_delivers_the_signal_on_the_applications_own_channel()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(_redis.ConnectionString);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = RedisChannel.Literal(ConfigurationChangeChannel.ForApplication("SERVICE-A"));
        await connection.GetSubscriber().SubscribeAsync(channel, (_, message) => received.TrySetResult(message!));

        var notifier = new RedisChangeNotifier(connection);
        await notifier.PublishAsync(new ConfigurationChangeSignal("SERVICE-A", "record-1"));

        var payload = await received.Task.WaitAsync(DeliveryTimeout);
        payload.Should().Contain("SERVICE-A").And.Contain("record-1");
    }

    [Fact]
    public async Task A_service_a_signal_is_never_delivered_to_a_service_b_subscriber()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(_redis.ConnectionString);
        var serviceBReceived = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serviceBChannel = RedisChannel.Literal(ConfigurationChangeChannel.ForApplication("SERVICE-B"));
        await connection.GetSubscriber().SubscribeAsync(serviceBChannel, (_, message) => serviceBReceived.TrySetResult(message!));

        var notifier = new RedisChangeNotifier(connection);
        await notifier.PublishAsync(new ConfigurationChangeSignal("SERVICE-A", "record-1"));

        var act = async () => await serviceBReceived.Task.WaitAsync(NonDeliveryWindow);
        await act.Should().ThrowAsync<TimeoutException>();
    }
}
