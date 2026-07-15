using ConfigReader.Core.Application.Messaging;

namespace ConfigReader.Core.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IChangeSubscriber"/> test double. Records which application it was bound to,
/// lets a test push a signal into the reader's handler (<see cref="PublishSignalAsync"/>), can be
/// told to fail on subscribe (to exercise the poll-only fallback), and tracks disposal.
/// </summary>
public sealed class FakeChangeSubscriber : IChangeSubscriber
{
    private Func<ConfigurationChangeSignal, CancellationToken, Task>? _onChange;

    public string? SubscribedApplicationName { get; private set; }

    public bool IsDisposed { get; private set; }

    public bool ThrowOnSubscribe { get; set; }

    public Task SubscribeAsync(
        string applicationName,
        Func<ConfigurationChangeSignal, CancellationToken, Task> onChange,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnSubscribe)
        {
            throw new InvalidOperationException("Simulated broker outage during subscribe.");
        }

        SubscribedApplicationName = applicationName;
        _onChange = onChange;
        return Task.CompletedTask;
    }

    /// <summary>Simulates a broker message arriving on the subscribed channel.</summary>
    public Task PublishSignalAsync(ConfigurationChangeSignal signal) =>
        _onChange?.Invoke(signal, CancellationToken.None) ?? Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
