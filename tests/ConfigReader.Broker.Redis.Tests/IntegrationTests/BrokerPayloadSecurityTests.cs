using System.Text.Json;
using ConfigReader.Core.Application.Messaging;
using FluentAssertions;
using StackExchange.Redis;

namespace ConfigReader.Broker.Redis.Tests.IntegrationTests;

/// <summary>
/// Wire-level verification of the anti cache-poisoning guarantee (CFG-9.3): inspect the exact bytes
/// that cross the broker and prove they are a value-free signal — only the application name and the
/// changed record's id, never a configuration value.
/// </summary>
[Collection(RedisCollection.Name)]
public sealed class BrokerPayloadSecurityTests
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(5);

    private readonly RedisContainerFixture _redis;

    public BrokerPayloadSecurityTests(RedisContainerFixture redis) => _redis = redis;

    [Fact]
    public async Task The_published_message_carries_only_the_application_and_record_id_never_a_value()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(_redis.ConnectionString);
        var raw = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = RedisChannel.Literal(ConfigurationChangeChannel.ForApplication("SERVICE-A"));
        await connection.GetSubscriber().SubscribeAsync(channel, (_, message) => raw.TrySetResult(message!));

        await new RedisChangeNotifier(connection)
            .PublishAsync(new ConfigurationChangeSignal("SERVICE-A", "SiteName"));

        var payload = await raw.Task.WaitAsync(DeliveryTimeout);

        using var document = JsonDocument.Parse(payload);
        var fieldNames = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        fieldNames.Should().BeEquivalentTo(new[] { "ApplicationName", "RecordId" },
            "the wire payload must be a pure signal with no field able to carry a configuration value");

        // The value that would matter in a real change (e.g. "soty.io") never appears on the wire.
        payload.Should().NotContainEquivalentOf("value");
        payload.Should().NotContain("soty.io");
    }
}
