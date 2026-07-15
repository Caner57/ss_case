using ConfigReader.Core.Application.Messaging;

namespace ConfigReader.Admin.Api.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IChangeNotifier"/> test double that records every published signal for
/// assertions, and can be told to fail so the best-effort publish path (a broker outage must not
/// fail the write) can be exercised deterministically.
/// </summary>
public sealed class RecordingChangeNotifier : IChangeNotifier
{
    private readonly object _gate = new();
    private readonly List<ConfigurationChangeSignal> _published = new();

    public bool ThrowOnPublish { get; set; }

    public IReadOnlyList<ConfigurationChangeSignal> Published
    {
        get
        {
            lock (_gate)
            {
                return _published.ToList();
            }
        }
    }

    public Task PublishAsync(
        ConfigurationChangeSignal signal,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("Simulated broker outage.");
        }

        lock (_gate)
        {
            _published.Add(signal);
        }

        return Task.CompletedTask;
    }
}
