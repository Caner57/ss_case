using ConfigReader.Broker.Redis;
using ConfigReader.Core.Application;
using ConfigReader.Storage.Mongo;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

// Sample consumer for SERVICE-B. Mirrors SERVICE-A but for a different tenant, which also proves the
// case's isolation requirement end to end: SERVICE-B only ever sees its own IsBasketEnabled key and
// never SERVICE-A's SiteName. Everything is env-driven — docker-compose injects the application name
// and connection strings.
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var applicationName = configuration["ConfigReader:ApplicationName"] ?? "SERVICE-B";
var mongoConnectionString = configuration.GetConnectionString("Mongo")
    ?? throw new InvalidOperationException(
        "ConnectionStrings__Mongo must be provided (e.g. via docker-compose environment).");
var refreshIntervalInMs = configuration.GetValue<int?>("ConfigReader:RefreshIntervalInMs") ?? 5000;
var redisConnectionString = configuration["Redis:ConnectionString"];

// Composition root: bind the library's Core ports to the concrete adapters. Core never references
// MongoDB or Redis — the host wires them here, then constructs the reader with the case's exact
// three-parameter signature.
ConfigurationReader.UseStoreFactory(new MongoConfigurationStoreFactory());

await using var broker = await ConnectBrokerAsync(redisConnectionString);
if (broker is not null)
{
    ConfigurationReader.UseChangeSubscriberFactory(new RedisChangeSubscriberFactory(broker));
}

await using var reader = new ConfigurationReader(applicationName, mongoConnectionString, refreshIntervalInMs);

using var shutdown = CreateShutdownSource();
Console.WriteLine(
    $"[{applicationName}] consumer started | refresh={refreshIntervalInMs}ms | "
    + $"broker={(broker is null ? "off (poll-only)" : "on (instant invalidation)")}");

using var ticker = new PeriodicTimer(TimeSpan.FromSeconds(2));
do
{
    // SiteName belongs to SERVICE-A; it must stay <absent> here — a live proof of tenant isolation
    // next to SERVICE-B's own IsBasketEnabled value.
    Console.WriteLine(
        $"[{applicationName}] {DateTime.UtcNow:HH:mm:ss}Z "
        + $"IsBasketEnabled={Describe<bool>(reader, "IsBasketEnabled")} "
        + $"SiteName(foreign)={Describe<string>(reader, "SiteName")}");
}
while (await WaitForTickAsync(ticker, shutdown.Token));

Console.WriteLine($"[{applicationName}] shutting down.");

static string Describe<T>(ConfigurationReader reader, string key)
{
    try
    {
        return reader.GetValue<T>(key)?.ToString() ?? "null";
    }
    catch (ConfigurationKeyNotFoundException)
    {
        return "<absent>";
    }
}

static async Task<IConnectionMultiplexer?> ConnectBrokerAsync(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return null;
    }

    // AbortOnConnectFail=false keeps a temporarily unreachable broker from crashing startup; the
    // reader falls back to polling until Redis returns (the broker is an optimization, never fatal).
    var options = ConfigurationOptions.Parse(connectionString);
    options.AbortOnConnectFail = false;
    return await ConnectionMultiplexer.ConnectAsync(options);
}

static CancellationTokenSource CreateShutdownSource()
{
    var source = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        source.Cancel();
    };
    AppDomain.CurrentDomain.ProcessExit += (_, _) => source.Cancel();
    return source;
}

static async Task<bool> WaitForTickAsync(PeriodicTimer timer, CancellationToken token)
{
    try
    {
        return await timer.WaitForNextTickAsync(token);
    }
    catch (OperationCanceledException)
    {
        return false;
    }
}
