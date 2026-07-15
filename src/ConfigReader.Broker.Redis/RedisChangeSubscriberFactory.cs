using ConfigReader.Core.Application.Messaging;
using StackExchange.Redis;

namespace ConfigReader.Broker.Redis;

/// <summary>
/// Builds <see cref="RedisChangeSubscriber"/> instances over a shared, pre-configured
/// <see cref="IConnectionMultiplexer"/>. Registered once at a consuming host's composition root via
/// <see cref="ConfigReader.Core.Application.ConfigurationReader.UseChangeSubscriberFactory"/> so the
/// library's three-parameter constructor can wire up push-based invalidation. The Redis endpoint is
/// baked in here (the reader's own constructor only knows the storage connection string), keeping
/// the broker an orthogonal, optional concern.
/// </summary>
public sealed class RedisChangeSubscriberFactory : IChangeSubscriberFactory
{
    private readonly IConnectionMultiplexer _connection;

    public RedisChangeSubscriberFactory(IConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    public IChangeSubscriber Create() => new RedisChangeSubscriber(_connection);
}
