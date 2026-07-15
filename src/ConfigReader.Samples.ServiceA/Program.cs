using ConfigReader.Broker.Redis;
using ConfigReader.Core.Application;
using ConfigReader.Storage.Mongo;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

// Sample consumer for SERVICE-A. Demonstrates the case's "read a live value while the service is
// running, without a restart" requirement: it constructs the library once and then prints the
// currently visible values on a loop. Any change made through the Admin API shows up on the next
// refresh tick (or instantly when the Redis broker is wired). Everything is env-driven — no
// hard-coded connection strings or application name (docker-compose injects them).
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var applicationName = configuration["ConfigReader:ApplicationName"] ?? "SERVICE-A";
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
    // MaxItemCount is seeded inactive for SERVICE-A, so it stays <absent> here — a live proof of the
    // IsActive filter alongside SiteName, the case's canonical GetValue<string>("SiteName") example.
    Console.WriteLine(
        $"[{applicationName}] {DateTime.UtcNow:HH:mm:ss}Z "
        + $"SiteName={Describe<string>(reader, "SiteName")} "
        + $"MaxItemCount={Describe<int>(reader, "MaxItemCount")}");
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
