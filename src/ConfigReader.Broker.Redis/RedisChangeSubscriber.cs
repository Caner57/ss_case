using ConfigReader.Core.Application.Messaging;
using StackExchange.Redis;

namespace ConfigReader.Broker.Redis;

/// <summary>
/// Redis Pub/Sub implementation of the <see cref="IChangeSubscriber"/> port. Binds to exactly one
/// application's literal channel <c>config-changes:{applicationName}</c> — there is no pattern
/// subscription, so a reader can never receive another tenant's signals (CFG-9.3 isolation).
/// <para>
/// Resilience (CFG-4.3): the underlying <see cref="IConnectionMultiplexer"/> auto-reconnects and
/// re-establishes this subscription after a transient broker outage, so instant invalidation
/// resumes on its own once the broker returns. A malformed payload is ignored rather than throwing.
/// </para>
/// </summary>
public sealed class RedisChangeSubscriber : IChangeSubscriber
{
    private readonly IConnectionMultiplexer _connection;
    private readonly bool _ownsConnection;

    private RedisChannel _channel;
    private bool _isSubscribed;
    private Func<ConfigurationChangeSignal, CancellationToken, Task>? _onChange;
    private int _disposed;

    public RedisChangeSubscriber(IConnectionMultiplexer connection, bool ownsConnection = false)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
        _ownsConnection = ownsConnection;
    }

    public async Task SubscribeAsync(
        string applicationName,
        Func<ConfigurationChangeSignal, CancellationToken, Task> onChange,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentNullException.ThrowIfNull(onChange);

        _onChange = onChange;
        _channel = RedisChannel.Literal(ConfigurationChangeChannel.ForApplication(applicationName));

        await _connection.GetSubscriber().SubscribeAsync(_channel, HandleMessage).ConfigureAwait(false);
        _isSubscribed = true;
    }

    private void HandleMessage(RedisChannel channel, RedisValue message)
    {
        var handler = _onChange;
        if (handler is null)
        {
            return;
        }

        // Never trust the wire: a malformed/garbage payload yields null and is ignored (CFG-9.3).
        var signal = ConfigurationChangeSignalSerializer.TryDeserialize(message);
        if (signal is null)
        {
            return;
        }

        // StackExchange invokes this synchronously; the handler contains its own errors (a refresh
        // failure keeps the last good snapshot), so fire-and-forget is safe here.
        _ = handler(signal, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (_isSubscribed)
        {
            try
            {
                await _connection.GetSubscriber().UnsubscribeAsync(_channel).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort teardown: a broker already gone cannot leak a server-side subscription.
            }
        }

        if (_ownsConnection)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
