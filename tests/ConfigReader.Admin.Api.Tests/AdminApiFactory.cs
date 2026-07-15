using ConfigReader.Admin.Api.Application;
using ConfigReader.Admin.Api.Tests.Fakes;
using ConfigReader.Core.Application;
using ConfigReader.Core.Application.Messaging;
using ConfigReader.Storage.Mongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace ConfigReader.Admin.Api.Tests;

/// <summary>
/// Hosts the Admin API in-process for integration tests, swapping the MongoDB adapter for an
/// <see cref="InMemoryConfigurationStore"/> so CRUD/validation/security behaviour can be verified
/// without external infrastructure. The shared store instance is exposed for assertions.
/// </summary>
public sealed class AdminApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKeyHeader = "X-Api-Key";
    public const string ValidApiKey = "test-admin-api-key";
    public const string AllowedOrigin = "http://localhost:5173";

    public InMemoryConfigurationStore Store { get; } = new();

    public InMemoryAuditLogWriter AuditLog { get; } = new();

    public RecordingChangeNotifier ChangeNotifier { get; } = new();

    public CapturedLogs Logs { get; } = new();

    private readonly string _environment;
    private readonly int? _writePermitLimit;

    public AdminApiFactory(string environment = "Development", int? writePermitLimit = null)
    {
        _environment = environment;
        _writePermitLimit = writePermitLimit;
    }

    /// <summary>Client that carries a valid API key so authenticated flows can be tested.</summary>
    public HttpClient CreateAuthedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyHeader, ValidApiKey);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        // UseSetting (unlike ConfigureAppConfiguration) is visible to the values Program.cs reads at
        // startup — the CORS origins and rate-limit options — not just to request-time reads.
        builder.UseSetting("ConnectionStrings:Mongo", "mongodb://localhost:27017/configdb");
        builder.UseSetting("AdminApi:ApiKey", ValidApiKey);
        builder.UseSetting("AdminApi:AllowedOrigins:0", AllowedOrigin);

        if (_writePermitLimit is { } permitLimit)
        {
            builder.UseSetting("AdminApi:WriteRateLimit:PermitLimit", permitLimit.ToString());
            builder.UseSetting("AdminApi:WriteRateLimit:WindowSeconds", "60");
        }

        builder.ConfigureLogging(logging => logging.AddProvider(new InMemoryLoggerProvider(Logs)));

        builder.ConfigureServices(services =>
        {
            RemoveMongoRegistrations(services);

            services.AddSingleton<IConfigurationManagementStore>(Store);
            services.AddSingleton<IConfigurationWriter>(Store);
            services.AddSingleton<IAuditLogWriter>(AuditLog);

            services.RemoveAll<IChangeNotifier>();
            services.AddSingleton<IChangeNotifier>(ChangeNotifier);
        });
    }

    private static void RemoveMongoRegistrations(IServiceCollection services)
    {
        services.RemoveAll<IConfigurationManagementStore>();
        services.RemoveAll<IConfigurationWriter>();
        services.RemoveAll<IConfigurationStore>();
        services.RemoveAll<IAuditLogWriter>();
        services.RemoveAll<MongoConfigurationStore>();
        services.RemoveAll<IMongoDatabase>();
    }
}
