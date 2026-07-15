namespace ConfigReader.Core.Application.Messaging;

/// <summary>
/// Null-object <see cref="IChangeNotifier"/> used when no broker is configured. Publishing becomes
/// a no-op so the write path never has to null-check the notifier, and the absence of a broker
/// stays a non-event (polling remains the freshness guarantee).
/// </summary>
public sealed class NullChangeNotifier : IChangeNotifier
{
    public static readonly NullChangeNotifier Instance = new();

    public Task PublishAsync(
        ConfigurationChangeSignal signal,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
