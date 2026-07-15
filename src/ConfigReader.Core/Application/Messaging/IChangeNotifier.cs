namespace ConfigReader.Core.Application.Messaging;

/// <summary>
/// Publish port (outbound). The Admin API raises a <see cref="ConfigurationChangeSignal"/> after a
/// successful write; the Redis adapter (ConfigReader.Broker.Redis) implements this. Publishing is
/// best-effort by contract: an implementation (and its callers) must never let a failed publish
/// tear down the originating operation. The broker is a latency optimization, not a source of
/// truth — polling (CFG-3.4) remains the primary freshness guarantee.
/// </summary>
public interface IChangeNotifier
{
    Task PublishAsync(ConfigurationChangeSignal signal, CancellationToken cancellationToken = default);
}
