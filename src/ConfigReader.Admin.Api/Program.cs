using System.Reflection;
using System.Threading.RateLimiting;
using ConfigReader.Admin.Api;
using ConfigReader.Admin.Api.Application;
using ConfigReader.Admin.Api.Authentication;
using ConfigReader.Admin.Api.Infrastructure;
using ConfigReader.Admin.Api.Middleware;
using ConfigReader.Broker.Redis;
using ConfigReader.Core.Application;
using ConfigReader.Core.Application.Messaging;
using ConfigReader.Core.Application.TypeConversion;
using ConfigReader.Core.Domain;
using ConfigReader.Storage.Mongo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ConfigReader Admin API",
        Version = "v1",
        Description = "Management API for the dynamic configuration store: list, create and update "
            + "configuration records across all applications."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    var apiKeyScheme = new OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationHandler.HeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Admin API key. Send it in the '" + ApiKeyAuthenticationHandler.HeaderName + "' header.",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = ApiKeyAuthenticationHandler.SchemeName
        }
    };
    options.AddSecurityDefinition(ApiKeyAuthenticationHandler.SchemeName, apiKeyScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [apiKeyScheme] = Array.Empty<string>() });
});

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });

// Fail-closed default: every endpoint (reads included) requires an authenticated caller unless
// it explicitly opts out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Composition root: bind the Core ports to the concrete MongoDB adapter. The connection
// string comes from configuration/environment (CFG-9.4), never hard-coded.
var mongoConnectionString = builder.Configuration.GetConnectionString("Mongo");
var storageOptions = new MongoStorageOptions();

builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton<IMongoDatabase>(_ =>
{
    var client = new MongoClient(mongoConnectionString);
    return client.GetDatabase(storageOptions.DatabaseName);
});
builder.Services.AddSingleton<MongoConfigurationStore>();
builder.Services.AddSingleton<IConfigurationStore>(sp => sp.GetRequiredService<MongoConfigurationStore>());
builder.Services.AddSingleton<IConfigurationManagementStore>(sp => sp.GetRequiredService<MongoConfigurationStore>());
builder.Services.AddSingleton<IConfigurationWriter>(sp => sp.GetRequiredService<MongoConfigurationStore>());

// Audit trail (CFG-9.2): every create/update is recorded in a separate, append-only Mongo
// collection. The actor is taken from the authenticated principal established by CFG-9.1.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpContextCurrentActor>();
builder.Services.AddSingleton<IAuditLogWriter>(sp =>
    new MongoAuditLogWriter(sp.GetRequiredService<IMongoDatabase>()));

// Change notifier (CFG-4.2): after a write, publish a value-free signal to the record's
// application channel so subscribers refresh without waiting for the poll interval. The broker is
// optional and never a single point of dependency — when no Redis endpoint is configured we bind
// the null-object notifier, and even when it is, AbortOnConnectFail=false keeps a broker outage
// from blocking API startup or failing writes.
var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
        redisOptions.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(redisOptions);
    });
    builder.Services.AddSingleton<IChangeNotifier, RedisChangeNotifier>();
}
else
{
    builder.Services.AddSingleton<IChangeNotifier>(NullChangeNotifier.Instance);
}

builder.Services.AddScoped<ConfigurationManagementService>();
builder.Services.AddSingleton(ValueConverterRegistry.CreateDefault());
builder.Services.AddSingleton<ConfigurationValidator>();

builder.Services.AddProblemDetails();

// CORS: only the configured browser origin(s) (the Admin Web app) may call the API. Never
// AllowAnyOrigin — a management surface must not be reachable from arbitrary sites.
const string BrowserCorsPolicy = "AdminWeb";
var allowedOrigins = builder.Configuration.GetSection("AdminApi:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(BrowserCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Rate limiting: throttle the write surface. Limits are configurable so tests and production can
// tune them; defaults are generous enough for interactive admin use.
var writePermitLimit = builder.Configuration.GetValue<int?>("AdminApi:WriteRateLimit:PermitLimit") ?? 60;
var writeWindowSeconds = builder.Configuration.GetValue<int?>("AdminApi:WriteRateLimit:WindowSeconds") ?? 60;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(RateLimitPolicies.Write, limiter =>
    {
        limiter.PermitLimit = writePermitLimit;
        limiter.Window = TimeSpan.FromSeconds(writeWindowSeconds);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();

// Outermost: turn any unhandled exception into a generic ProblemDetails (no internal detail leaks)
// while logging the full error internally.
app.UseExceptionHandler(handler => handler.Run(ProblemDetailsExceptionHandler.HandleAsync));
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(BrowserCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

// First-run seeding of the case's demo dataset (CFG-8.2). Idempotent — the seeder inserts only when
// the collection is empty, so restarts and a persisted volume never duplicate data. The Admin API is
// the read-write service, so it (not the read-only sample consumers) owns this. A storage outage at
// startup is logged and swallowed: the API must still come up and self-heal on the next write.
await SeedDemoDataAsync(app);

app.Run();

static async Task SeedDemoDataAsync(WebApplication app)
{
    try
    {
        var database = app.Services.GetRequiredService<IMongoDatabase>();
        var options = app.Services.GetRequiredService<MongoStorageOptions>();
        var seeder = new MongoConfigurationSeeder(database, options);
        var inserted = await seeder.SeedAsync(CaseSeedData.Records);
        app.Logger.LogInformation("Configuration seeding complete: {InsertedCount} record(s) inserted.", inserted);
    }
    catch (Exception error)
    {
        app.Logger.LogWarning(error, "Configuration seeding skipped: storage unreachable at startup.");
    }
}

/// <summary>Exposed so integration tests can host the API via <c>WebApplicationFactory</c>.</summary>
public partial class Program;
