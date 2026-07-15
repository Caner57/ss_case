namespace ConfigReader.Core.Application.Messaging;

/// <summary>
/// Subscribe port (inbound), implemented by the Redis adapter. A subscriber can only ever bind to
/// a <em>single</em> application's channel: <see cref="SubscribeAsync"/> takes exactly one
/// <c>applicationName</c> and there is no pattern/wildcard overload, so a library instance cannot
/// listen to another tenant's channel (CFG-9.3 channel isolation).
/// <para>
/// The handler receives only a <see cref="ConfigurationChangeSignal"/> and is expected to re-read
/// the authoritative values from storage rather than trust the payload. Implementations must fail
/// soft: if the broker is unreachable the library falls back to polling, so subscription failures
/// are surfaced (e.g. thrown from <see cref="SubscribeAsync"/>) for the caller to contain, never
/// silently corrupting state.
/// </para>
/// </summary>
public interface IChangeSubscriber : IAsyncDisposable
{
    Task SubscribeAsync(
        string applicationName,
        Func<ConfigurationChangeSignal, CancellationToken, Task> onChange,
        CancellationToken cancellationToken = default);
}
