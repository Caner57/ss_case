# ConfigReader — Dynamic Configuration Library

A .NET 8 dynamic configuration library with a MongoDB store, a Redis change broker, a management
Web API, a React admin UI and two sample consumer services — all runnable with a single
`docker compose up`.

The library lets any .NET application read configuration values **while it is running**, with no
deployment, restart or recycle needed when a value changes. A value updated through the admin
surface is picked up by the consuming service on its next refresh — or **instantly** when the Redis
broker is wired.

---

## Architecture

Dependencies flow **inward** (Clean Architecture): the domain/application core knows nothing about
MongoDB, Redis or ASP.NET. Infrastructure and hosts depend on the core, never the reverse.

```
                         ┌───────────────────────────────────────────┐
                         │            ConfigReader.Core               │
                         │  (pure .NET 8 class library — the "dll")   │
                         │                                            │
   consumer host ───────►│  ConfigurationReader(appName, connStr, ms) │
   (any host: console,   │  T GetValue<T>(key)                        │
    web, web api, WCF...) │                                            │
                         │  Ports (interfaces):                       │
                         │   • IConfigurationStore                    │
                         │   • IChangeSubscriber / IChangeNotifier    │
                         │   • IConfigurationStoreFactory             │
                         └──────▲───────────────────▲─────────────────┘
                                │ implements        │ implements
                 ┌──────────────┴───────┐   ┌───────┴────────────────┐
                 │ ConfigReader.Storage │   │ ConfigReader.Broker    │
                 │       .Mongo         │   │       .Redis           │
                 │  MongoConfiguration- │   │  RedisChangeNotifier   │
                 │  Store / -Factory    │   │  RedisChangeSubscriber │
                 └──────────▲───────────┘   └───────▲────────────────┘
                            │                       │
      ┌─────────────────────┴───────────┐           │  publish/subscribe
      │      ConfigReader.Admin.Api      │           │  (config-changes:{app})
      │  CRUD, auth, validation, audit,  │───────────┘
      │  Swagger — writes to Mongo,      │
      │  publishes change signals        │
      └─────────────────▲────────────────┘
                        │ REST (X-Api-Key)
      ┌─────────────────┴────────────────┐        ┌──────────────────────────────┐
      │      ConfigReader.Admin.Web       │        │  Samples.ServiceA / ServiceB │
      │        React (Vite) UI            │        │  real ConfigurationReader    │
      └───────────────────────────────────┘        │  consumers (SERVICE-A/-B)    │
                                                    └──────────────────────────────┘
```

### Data flow (end to end)

1. An operator changes a record in the **Admin UI** (or via `curl`), which calls the **Admin API**.
2. The API **writes to MongoDB** and **publishes a value-free change signal** to the record's Redis
   channel (`config-changes:{applicationName}`).
3. A **consumer service** holding a `ConfigurationReader`:
   - is subscribed to its own channel, so it **re-reads from storage immediately** on the signal; and
   - independently **polls** storage every `refreshTimerIntervalInMs` as the primary guarantee.
4. `GetValue<T>(key)` now returns the new value — the running service never restarted.

### Projects

| Project | Layer | Responsibility |
|---|---|---|
| `ConfigReader.Core` | Domain + Application | The library. `ConfigurationReader`, ports, type conversion, cache. **No infra/host NuGet references.** |
| `ConfigReader.Storage.Mongo` | Infrastructure | `IConfigurationStore` + `IConfigurationStoreFactory` MongoDB adapters, seeder. |
| `ConfigReader.Broker.Redis` | Infrastructure | `IChangeNotifier` / `IChangeSubscriber` Redis Pub/Sub adapters. |
| `ConfigReader.Admin.Api` | Presentation | Management CRUD REST API (auth, validation, audit, Swagger, change publishing). |
| `ConfigReader.Admin.Web` | Presentation | React (Vite) admin UI; client-side name filter. |
| `ConfigReader.Samples.ServiceA` / `ServiceB` | Sample host | Real consumers that read live values in a loop. |

---

## Using the library in your project

Add a reference to `ConfigReader.Core` and to a storage adapter (`ConfigReader.Storage.Mongo`).
Then, **once at your composition root**, register the store factory and construct a reader with the
case's exact three-parameter signature:

```csharp
using ConfigReader.Core.Application;
using ConfigReader.Storage.Mongo;

// 1. Composition root (run once at startup): teach Core how to turn a connection string into a
//    store, without Core ever referencing MongoDB.
ConfigurationReader.UseStoreFactory(new MongoConfigurationStoreFactory());

// 2. Optional: wire push-based (instant) invalidation via the Redis broker. Omit this and the
//    library runs on polling only — the broker is never a single point of dependency.
//    ConfigurationReader.UseChangeSubscriberFactory(new RedisChangeSubscriberFactory(redisConnection));

// 3. Construct the reader (at most three parameters — the library's only public entry point).
var reader = new ConfigurationReader(
    applicationName: "SERVICE-A",
    connectionString: "mongodb://localhost:27017/configdb",
    refreshTimerIntervalInMs: 5000);

// 4. Read strongly-typed values anywhere, anytime — no restart needed when they change.
string siteName = reader.GetValue<string>("SiteName");   // => "soty.io"
int    maxItems = reader.GetValue<int>("MaxItemCount");
bool   basket   = reader.GetValue<bool>("IsBasketEnabled");
```

A complete, working example is `ConfigReader.Samples.ServiceA/Program.cs`: it reads its application
name and connection strings from the environment, wires the Mongo store factory and the optional
Redis subscriber factory, and prints the currently visible values on a loop.

- **`Value` is stored as a string** and converted to `T` at read time based on the record's `Type`
  (`string`, `int`, `double`, `bool`).
- **Only `IsActive = true` records** for the reader's **own `ApplicationName`** are ever visible —
  no cross-tenant leakage.
- **Storage fallback:** if the store becomes unreachable, `GetValue<T>` keeps serving the last
  successful snapshot instead of throwing; the background loop self-heals on the next success.
- The reader is `IAsyncDisposable` — dispose it on shutdown to stop the refresh loop and release the
  broker subscription. The minimum refresh interval is `1000` ms.

### Host-independent usage (web / web api / WCF / CoreWCF)

`ConfigReader.Core` is a **pure .NET 8 class library with no dependency on any host framework**
(no ASP.NET, no WCF, no hosting packages — verifiable in `ConfigReader.Core.csproj`). It is therefore
consumed **identically from any host** using the same three-parameter constructor and `GetValue<T>`:

- **Console / worker service** — see the sample consumers.
- **ASP.NET Core web app / Web API** — register a single `ConfigurationReader` in DI (as a singleton)
  and inject it into controllers/services; the Admin API in this repo is itself an ASP.NET host.
- **WCF / CoreWCF** — construct one `ConfigurationReader` in the service host (e.g. a singleton
  service instance or a static composition root) and call `GetValue<T>` from your operation
  contracts. **No separate WCF sample project is required**: because the core carries no host
  coupling, hosting it under CoreWCF is the same two lines (`UseStoreFactory` + `new
  ConfigurationReader(...)`) shown above. This is how the case's "the dll must be reachable from web,
  wcf, web api — every kind of project" requirement is satisfied.

---

## Running the whole ecosystem with docker-compose

**Prerequisites:** Docker + Docker Compose v2.

```bash
# 1. Provide local environment values (credentials, ports, admin key) in .env. .env is gitignored.

# 2. Build and start all six services (mongo, redis, config-api, config-ui, service-a, service-b).
docker compose build
docker compose up -d --wait --wait-timeout 90
```

On first start the Admin API idempotently seeds the case's demo dataset (`SiteName`,
`IsBasketEnabled`, `MaxItemCount`). Data lives in the `mongo-data` volume, so it survives
`docker compose down` / `up`.

| Service | URL (host) | Notes |
|---|---|---|
| Admin API (`config-api`) | http://localhost:8080 | Requires the `X-Api-Key` header. Swagger UI in Development only. |
| Admin UI (`config-ui`) | http://localhost:8081 | Admin key is read from `.env` automatically; name filter runs client-side. |
| `mongo` / `redis` | not published | Reachable only on the internal compose network. |

Update a value and watch a consumer pick it up:

```bash
# List records (find the SiteName id):
curl -s -H "X-Api-Key: $ADMIN_API_KEY" http://localhost:8080/api/configurations

# Update SERVICE-A's SiteName:
curl -X PUT -H "X-Api-Key: $ADMIN_API_KEY" -H "Content-Type: application/json" \
  -d '{"name":"SiteName","type":"string","value":"updated.soty.io","isActive":true,"applicationName":"SERVICE-A"}' \
  http://localhost:8080/api/configurations/<id>

# Observe SERVICE-A reflect the new value (instantly, via the Redis broker):
docker compose logs -f service-a

# Tidy up (add -v to also drop the mongo volume):
docker compose down
```

---

## How the case requirements are met

| Requirement | Where |
|---|---|
| At most 3 constructor params; `T GetValue<T>(key)` | `ConfigurationReader` public surface. |
| `GetValue<string>("SiteName") == "soty.io"` | Seeded data; demonstrated live in `service-a` logs. |
| Only `IsActive = 1` records returned | Server-side filter in `MongoConfigurationStore.GetActiveRecordsAsync`. `MaxItemCount` (inactive) stays `<absent>` in `service-a`. |
| Per-application isolation (no cross-tenant leak) | Filtered by `ApplicationName`; `service-b` never sees `service-a`'s `SiteName`. |
| Works when storage is down (fallback) | Last-good snapshot kept; failures surfaced, never thrown to the consumer. |
| Periodic refresh, no restart needed | `PeriodicTimer` loop + atomic snapshot swap (concurrency-safe cache). |
| **Extra:** message broker for instant updates | Redis Pub/Sub (`ConfigReader.Broker.Redis`); polling remains the primary guarantee. |
| **Extra:** async/await & TPL | `PeriodicTimer`, `IAsyncDisposable`, async store/broker calls throughout. |
| **Extra:** TDD | xUnit + FluentAssertions + NSubstitute + Testcontainers across all layers. |
| **Extra:** runnable ecosystem | `docker-compose.yml` (mongo, redis, config-api, config-ui, service-a, service-b). |
| **Extra:** documentation | This README. |

---

## Security & operations (Admin API)

### TLS / HTTPS (production requirement)

In the local `docker-compose` environment the Admin API is reached over plain HTTP for
convenience. **In production, HTTPS is mandatory** — the API carries an admin API key and can
change the live behaviour of every service, so the transport must be encrypted end to end.

Enable it in one of two standard ways:

- **Reverse proxy / ingress termination (recommended):** terminate TLS at nginx / Traefik / a
  cloud load balancer and forward to the API over the internal network. Configure
  `ForwardedHeaders` so the app sees the original scheme.
- **Kestrel direct TLS:** provide a certificate via `ASPNETCORE_Kestrel__Certificates__Default__Path`
  and `...__Password` (or `ASPNETCORE_URLS=https://+:443`) and keep `UseHttpsRedirection` active.

Never expose the Admin API on plain HTTP outside a trusted local network.

### Authentication (CFG-9.1)

All endpoints (reads included) require a valid `X-Api-Key` header. The key is read from
configuration/environment (`AdminApi__ApiKey`) — never hard-coded — and the API fails closed
(401) if it is unset.

### CORS (CFG-9.6)

Only the browser origins listed in `AdminApi:AllowedOrigins` may call the API. `AllowAnyOrigin`
is never used.

### Rate limiting (CFG-9.6)

Write endpoints (`POST`/`PUT`) are throttled by a fixed-window limiter. Tune it with
`AdminApi:WriteRateLimit:PermitLimit` and `AdminApi:WriteRateLimit:WindowSeconds`.

### Error responses (CFG-9.6)

Unhandled exceptions return a generic `ProblemDetails` with a `correlationId`; stack traces,
internal messages and connection strings are logged internally and never sent to the client.
Baseline security headers (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`) are added
to every response.
