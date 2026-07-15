using ConfigReader.Core.Application.Messaging;
using StackExchange.Redis;

namespace ConfigReader.Broker.Redis;

/// <summary>
/// Redis Pub/Sub implementation of the <see cref="IChangeNotifier"/> publish port. Publishes a
/// value-free <see cref="ConfigurationChangeSignal"/> to the application-specific literal channel
/// <c>config-changes:{applicationName}</c> so only that application's subscribers are woken.
/// <para>
/// The channel is always a <see cref="RedisChannel.Literal"/> (never a pattern), which keeps the
/// publish path aligned with the isolation guarantee: a signal for SERVICE-A cannot fan out to
/// SERVICE-B listeners.
/// </para>
/// </summary>
public sealed class RedisChangeNotifier : IChangeNotifier
{
    private readonly IConnectionMultiplexer _connection;

    public RedisChangeNotifier(IConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    public async Task PublishAsync(
        ConfigurationChangeSignal signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);

        var channel = RedisChannel.Literal(ConfigurationChangeChannel.ForApplication(signal.ApplicationName));
        var payload = ConfigurationChangeSignalSerializer.Serialize(signal);

        await _connection.GetSubscriber().PublishAsync(channel, payload).ConfigureAwait(false);
    }
}
