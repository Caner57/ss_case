using ConfigReader.Core.Application.Caching;
using ConfigReader.Core.Application.Messaging;
using ConfigReader.Core.Application.TypeConversion;
using ConfigReader.Core.Domain;

namespace ConfigReader.Core.Application;

/// <summary>
/// The library's single public entry point. Honours the case's contract of at most three
/// constructor parameters: <c>new ConfigurationReader(applicationName, connectionString,
/// refreshTimerIntervalInMs)</c>.
/// <para>
/// Composition-root decision (CFG-3.1): the public constructor must turn a connection string
/// into an <see cref="IConfigurationStore"/>, but Core is forbidden from referencing a concrete
/// storage technology. It therefore resolves the store through a process-wide
/// <see cref="IConfigurationStoreFactory"/> registered once at startup via
/// <see cref="UseStoreFactory"/> (the Mongo adapter registers the real factory). An internal
/// constructor that takes an already-built store is used by tests and DI-based hosts, keeping
/// the class fully testable while the public surface stays at three parameters.
/// </para>
/// <para>
/// Initial-load decision (CFG-3.1): the first fetch runs synchronously (best-effort) in the
/// constructor so the object is usable the moment it is created — matching the case example
/// <c>GetValue&lt;string&gt;("SiteName") == "soty.io"</c>. A storage failure at startup is never
/// propagated to the caller: the cache simply stays empty and the background loop (CFG-3.4)
/// self-heals once storage is reachable.
/// </para>
/// </summary>
public sealed class ConfigurationReader : IConfigurationReader, IDisposable, IAsyncDisposable
{
    /// <summary>Lower bound for the refresh period. Values below this are raised to it so a tiny
    /// interval cannot hammer the store thousands of times per second.</summary>
    public const int MinimumRefreshIntervalInMs = 1000;

    private static IConfigurationStoreFactory? _storeFactory;
    private static IChangeSubscriberFactory? _subscriberFactory;

    private readonly string _applicationName;
    private readonly IConfigurationStore _store;
    private readonly IChangeSubscriber? _changeSubscriber;
    private readonly SnapshotConfigurationCache _cache = new();
    private readonly ValueConverterRegistry _converters = ValueConverterRegistry.CreateDefault();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Action<Exception>? _onRefreshError;
    private readonly Task _refreshLoop;
    private readonly Task _subscriptionLoop;
    private int _disposed;
    private int _refreshFailureCount;

    /// <summary>Registers the process-wide factory that turns connection strings into stores.
    /// Called once at the composition root (e.g. from the Mongo adapter).</summary>
    public static void UseStoreFactory(IConfigurationStoreFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _storeFactory = factory;
    }

    internal static void ResetStoreFactory() => _storeFactory = null;

    /// <summary>Registers the process-wide factory that builds broker subscribers, letting the
    /// three-parameter public constructor wire up push-based invalidation without Core referencing
    /// a concrete broker. Entirely optional: when unset, the library runs on polling only and the
    /// broker never becomes a single point of dependency (CFG-4.3).</summary>
    public static void UseChangeSubscriberFactory(IChangeSubscriberFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _subscriberFactory = factory;
    }

    internal static void ResetChangeSubscriberFactory() => _subscriberFactory = null;

    public ConfigurationReader(string applicationName, string connectionString, int refreshTimerIntervalInMs)
        : this(applicationName, ResolveStore(connectionString), refreshTimerIntervalInMs, changeSubscriber: ResolveSubscriber())
    {
    }

    internal ConfigurationReader(
        string applicationName,
        IConfigurationStore store,
        int refreshTimerIntervalInMs,
        Action<Exception>? onRefreshError = null,
        IChangeSubscriber? changeSubscriber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentNullException.ThrowIfNull(store);

        _applicationName = applicationName;
        _store = store;
        _changeSubscriber = changeSubscriber;
        _onRefreshError = onRefreshError;
        RefreshIntervalInMs = NormalizeInterval(refreshTimerIntervalInMs);

        LoadInitialSnapshot();
        _refreshLoop = RunRefreshLoopAsync(_shutdown.Token);
        _subscriptionLoop = StartChangeSubscription();
    }

    internal int RefreshIntervalInMs { get; }

    /// <summary>True once at least one refresh has successfully populated the cache. While false,
    /// the library is running on an empty snapshot (startup before storage was reachable).</summary>
    public bool HasLoadedSuccessfully { get; private set; }

    /// <summary>The most recent refresh failure, or null if the last refresh succeeded. Surfaces
    /// storage outages to the consumer instead of swallowing them silently (CFG-3.5).</summary>
    public Exception? LastRefreshError { get; private set; }

    /// <summary>Number of refresh iterations that failed since construction.</summary>
    public int RefreshFailureCount => Volatile.Read(ref _refreshFailureCount);

    /// <summary>The most recent broker (subscribe) failure, or null. Surfaces broker outages for
    /// observability while the library keeps working on polling — the broker is never fatal.</summary>
    public Exception? LastBrokerError { get; private set; }

    public T GetValue<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var record = _cache.Find(key)
            ?? throw new ConfigurationKeyNotFoundException(key, _applicationName);

        var configurationType = ConfigurationTypeResolver.Resolve(record.Type);
        var converted = _converters.Convert(configurationType, record.Value);

        if (converted is T typedValue)
        {
            return typedValue;
        }

        throw new ConfigurationTypeMismatchException(key, typeof(T), converted.GetType());
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _shutdown.Cancel();

        try
        {
            await _refreshLoop.ConfigureAwait(false);
            await _subscriptionLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_changeSubscriber is not null)
            {
                await _changeSubscriber.DisposeAsync().ConfigureAwait(false);
            }

            _shutdown.Dispose();
        }
    }

    /// <summary>Runs one fetch-and-swap cycle. A storage failure is contained (the last good
    /// snapshot is kept, per CFG-3.5) so a single bad iteration never tears down the loop.</summary>
    internal async Task RefreshOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var records = await _store
                .GetActiveRecordsAsync(_applicationName, cancellationToken)
                .ConfigureAwait(false);
            _cache.Replace(records);
            HasLoadedSuccessfully = true;
            LastRefreshError = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            RecordRefreshFailure(error);
        }
    }

    /// <summary>Keeps the last successful snapshot in place and records the failure so it is
    /// observable (property + optional sink) rather than silently swallowed. The consumer is
    /// never brought down: <see cref="GetValue{T}"/> keeps serving the last good values, and the
    /// loop re-syncs automatically on the next successful refresh.</summary>
    private void RecordRefreshFailure(Exception error)
    {
        Interlocked.Increment(ref _refreshFailureCount);
        LastRefreshError = error;
        _onRefreshError?.Invoke(error);
    }

    private void LoadInitialSnapshot() =>
        Task.Run(() => RefreshOnceAsync()).GetAwaiter().GetResult();

    /// <summary>Binds to this application's change channel when a broker subscriber is present. Any
    /// failure is contained: the broker is an optimization on top of polling (CFG-4.3), never a
    /// prerequisite, so a subscribe error degrades silently to poll-only rather than propagating.</summary>
    private Task StartChangeSubscription()
    {
        if (_changeSubscriber is null)
        {
            return Task.CompletedTask;
        }

        return SubscribeSafelyAsync(_shutdown.Token);
    }

    private async Task SubscribeSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _changeSubscriber!
                .SubscribeAsync(_applicationName, OnChangeSignalAsync, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            LastBrokerError = error;
            _onRefreshError?.Invoke(error);
        }
    }

    /// <summary>Reacts to a broker change signal. Two guarantees hold here (CFG-9.3): the signal is
    /// only honoured for this reader's own application (a signal for another application is ignored,
    /// not applied), and its payload is never trusted as data — the authoritative values are always
    /// re-read from storage, so a hostile broker can at most force a re-read, never inject a value.</summary>
    private async Task OnChangeSignalAsync(ConfigurationChangeSignal signal, CancellationToken cancellationToken)
    {
        if (signal is null || !string.Equals(signal.ApplicationName, _applicationName, StringComparison.Ordinal))
        {
            return;
        }

        await RefreshOnceAsync(_shutdown.Token).ConfigureAwait(false);
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(RefreshIntervalInMs));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await RefreshOnceAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static int NormalizeInterval(int refreshTimerIntervalInMs) =>
        Math.Max(refreshTimerIntervalInMs, MinimumRefreshIntervalInMs);

    private static IConfigurationStore ResolveStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var factory = _storeFactory
            ?? throw new InvalidOperationException(
                "No IConfigurationStoreFactory has been registered. Call " +
                "ConfigurationReader.UseStoreFactory(...) at the composition root " +
                "(the ConfigReader.Storage.Mongo adapter provides the concrete factory) " +
                "before constructing a ConfigurationReader from a connection string.");

        return factory.Create(connectionString);
    }

    /// <summary>Builds a broker subscriber if a factory is registered, otherwise null (poll-only).
    /// A construction failure is contained so a misconfigured broker never blocks the reader.</summary>
    private static IChangeSubscriber? ResolveSubscriber()
    {
        try
        {
            return _subscriberFactory?.Create();
        }
        catch
        {
            return null;
        }
    }
}
