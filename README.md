# ConfigReader — Dinamik Konfigürasyon Kütüphanesi

MongoDB depolama, Redis değişiklik yayıncısı (broker), bir yönetim Web API'si, bir React admin
arayüzü ve iki örnek tüketici (consumer) servisiyle birlikte gelen; tamamı tek bir
`docker compose up` komutuyla çalıştırılabilen bir .NET 8 dinamik konfigürasyon kütüphanesi.

Bu kütüphane, herhangi bir .NET uygulamasının konfigürasyon değerlerini **çalışırken** okumasına
izin verir; bir değer değiştiğinde deployment, restart veya recycle gerekmez. Admin arayüzü
üzerinden güncellenen bir değer, tüketici servis tarafından bir sonraki yenilemede — Redis broker
bağlıysa **anında** — algılanır.

---

## Mimari

Bağımlılıklar **içe doğru** akar (Clean Architecture): domain/application çekirdeği MongoDB,
Redis veya ASP.NET hakkında hiçbir şey bilmez. Infrastructure ve host'lar çekirdeğe bağımlıdır,
asla tersi değil.

```
                         ┌───────────────────────────────────────────┐
                         │            ConfigReader.Core               │
                         │  (saf .NET 8 class library — "dll")        │
                         │                                            │
   tüketici host ───────►│  ConfigurationReader(appName, connStr, ms) │
   (herhangi bir host:   │  T GetValue<T>(key)                        │
    console, web,        │                                            │
    web api, WCF...)     │  Portlar (arayüzler):                      │
                         │   • IConfigurationStore                    │
                         │   • IChangeSubscriber / IChangeNotifier    │
                         │   • IConfigurationStoreFactory             │
                         └──────▲───────────────────▲─────────────────┘
                                │ implement eder     │ implement eder
                 ┌──────────────┴───────┐   ┌───────┴────────────────┐
                 │ ConfigReader.Storage │   │ ConfigReader.Broker    │
                 │       .Mongo         │   │       .Redis           │
                 │  MongoConfiguration- │   │  RedisChangeNotifier   │
                 │  Store / -Factory    │   │  RedisChangeSubscriber │
                 └──────────▲───────────┘   └───────▲────────────────┘
                            │                       │
      ┌─────────────────────┴───────────┐           │  publish/subscribe
      │      ConfigReader.Admin.Api      │           │  (config-changes:{app})
      │  CRUD, auth, doğrulama, audit,   │───────────┘
      │  Swagger — Mongo'ya yazar,       │
      │  değişiklik sinyali yayınlar     │
      └─────────────────▲────────────────┘
                        │ REST (X-Api-Key)
      ┌─────────────────┴────────────────┐        ┌──────────────────────────────┐
      │      ConfigReader.Admin.Web       │        │  Samples.ServiceA / ServiceB │
      │        React (Vite) UI            │        │  gerçek ConfigurationReader  │
      └───────────────────────────────────┘        │  tüketicileri (SERVICE-A/-B) │
                                                    └──────────────────────────────┘
```

### Veri akışı (uçtan uca)

1. Bir operatör **Admin UI**'da (veya `curl` ile) bir kaydı değiştirir; bu da **Admin API**'yi
   çağırır.
2. API **MongoDB'ye yazar** ve kaydın Redis kanalına (`config-changes:{applicationName}`) **değer
   içermeyen bir değişiklik sinyali yayınlar**.
3. Bir `ConfigurationReader` tutan **tüketici servis**:
   - kendi kanalına abone olduğu için sinyal geldiğinde **depodan hemen yeniden okur**; ve
   - birincil garanti olarak, depoyu bağımsız şekilde her `refreshTimerIntervalInMs`'de bir
     **poll'lar**.
4. `GetValue<T>(key)` artık yeni değeri döndürür — çalışan servis hiç yeniden başlatılmamıştır.

### Projeler

| Proje | Katman | Sorumluluk |
|---|---|---|
| `ConfigReader.Core` | Domain + Application | Kütüphanenin kendisi. `ConfigurationReader`, portlar, tip dönüşümü, cache. **Infra/host NuGet referansı yoktur.** |
| `ConfigReader.Storage.Mongo` | Infrastructure | `IConfigurationStore` + `IConfigurationStoreFactory` MongoDB adaptörleri, seeder. |
| `ConfigReader.Broker.Redis` | Infrastructure | `IChangeNotifier` / `IChangeSubscriber` Redis Pub/Sub adaptörleri. |
| `ConfigReader.Admin.Api` | Presentation | Yönetim CRUD REST API'si (auth, doğrulama, audit, Swagger, değişiklik yayını). |
| `ConfigReader.Admin.Web` | Presentation | React (Vite) admin arayüzü; client-side isim filtresi. |
| `ConfigReader.Samples.ServiceA` / `ServiceB` | Örnek host | Canlı değerleri döngü içinde okuyan gerçek tüketiciler. |

---

## Kütüphaneyi kendi projenizde kullanma

`ConfigReader.Core`'a ve bir depolama adaptörüne (`ConfigReader.Storage.Mongo`) referans ekleyin.
Ardından, **composition root'unuzda yalnızca bir kez**, store factory'sini kaydedin ve case'in
tam üç parametreli imzasıyla bir reader oluşturun:

```csharp
using ConfigReader.Core.Application;
using ConfigReader.Storage.Mongo;

// 1. Composition root (başlangıçta bir kez çalıştırılır): Core'a, bir connection string'i
//    Core'un MongoDB'ye hiç referans vermeden nasıl bir store'a çevireceğini öğretir.
ConfigurationReader.UseStoreFactory(new MongoConfigurationStoreFactory());

// 2. Opsiyonel: Redis broker üzerinden push-tabanlı (anlık) invalidation bağlayın. Bu satır
//    atlanırsa kütüphane yalnızca polling ile çalışır — broker hiçbir zaman tek bir bağımlılık
//    noktası (single point of dependency) olmaz.
//    ConfigurationReader.UseChangeSubscriberFactory(new RedisChangeSubscriberFactory(redisConnection));

// 3. Reader'ı oluşturun (en fazla üç parametre — kütüphanenin tek public giriş noktası).
var reader = new ConfigurationReader(
    applicationName: "SERVICE-A",
    connectionString: "mongodb://localhost:27017/configdb",
    refreshTimerIntervalInMs: 5000);

// 4. Güçlü tipli değerleri her yerde, her an okuyun — değiştiklerinde restart gerekmez.
string siteName = reader.GetValue<string>("SiteName");   // => "soty.io"
int    maxItems = reader.GetValue<int>("MaxItemCount");
bool   basket   = reader.GetValue<bool>("IsBasketEnabled");
```

Eksiksiz, çalışan bir örnek `ConfigReader.Samples.ServiceA/Program.cs` dosyasındadır: uygulama
adını ve connection string'lerini ortam değişkenlerinden okur, Mongo store factory'sini ve
opsiyonel Redis subscriber factory'sini bağlar ve o an görünen değerleri döngü içinde yazdırır.

- **`Value` string olarak saklanır** ve okuma anında kaydın `Type` alanına göre (`string`, `int`,
  `double`, `bool`) `T`'ye dönüştürülür.
- Yalnızca reader'ın **kendi `ApplicationName`**'ine ait ve **`IsActive = true`** olan kayıtlar
  görünür — tenant'lar arası sızıntı yoktur.
- **Depolama fallback'i:** store erişilemez hale gelirse `GetValue<T>`, hata fırlatmak yerine son
  başarılı anlık görüntüyü (snapshot) sunmaya devam eder; arka plan döngüsü bir sonraki başarıda
  kendini iyileştirir (self-heal).
- Reader `IAsyncDisposable`'dır — refresh döngüsünü durdurmak ve broker aboneliğini serbest
  bırakmak için kapanışta dispose edin. Minimum refresh aralığı `1000` ms'dir.

### Host'tan bağımsız kullanım (web / web api / WCF / CoreWCF)

`ConfigReader.Core`, **herhangi bir host framework'üne bağımlılığı olmayan saf bir .NET 8 class
library'sidir** (ASP.NET yok, WCF yok, hosting paketi yok — `ConfigReader.Core.csproj` üzerinden
doğrulanabilir). Bu nedenle **herhangi bir host'tan aynı şekilde**, aynı üç parametreli
constructor ve `GetValue<T>` ile tüketilir:

- **Console / worker service** — örnek tüketicilere bakın.
- **ASP.NET Core web app / Web API** — DI'da tek bir `ConfigurationReader`'ı (singleton olarak)
  kaydedin ve controller/servislere enjekte edin; bu repodaki Admin API'nin kendisi de bir ASP.NET
  host'tur.
- **WCF / CoreWCF** — servis host'unda tek bir `ConfigurationReader` oluşturun (ör. bir singleton
  servis örneği veya statik bir composition root) ve operation contract'larınızdan `GetValue<T>`'yi
  çağırın. **Ayrı bir WCF örnek projesine gerek yoktur**: çekirdek hiçbir host bağımlılığı
  taşımadığından, onu CoreWCF altında host etmek yukarıda gösterilen aynı iki satırdır
  (`UseStoreFactory` + `new ConfigurationReader(...)`). Case'in "dll'in web, wcf, web api — her
  türlü projeden erişilebilir olmalı" isteri bu şekilde karşılanır.

---

## Tüm ekosistemi docker-compose ile çalıştırma

**Ön koşullar:** Docker + Docker Compose v2. Yerel .NET SDK veya Node kurulumuna gerek yoktur;
her imaj Docker içinde kaynak koddan build edilir.

```bash
# 1. Repoyu klonlayın ve solution kök dizinine girin.
git clone <repo-url>
cd code

# 2. Altı servisin tamamını build edip başlatın (mongo, redis, config-api, config-ui, service-a, service-b).
docker compose build
docker compose up -d --wait --wait-timeout 90
```

`.env` dosyası, yerel-geliştirme-amaçlı örnek kimlik bilgileriyle repoda commit edilmiş halde
gelir; böylece taze bir klonda ek bir kurulum adımı gerekmeden çalışır. **Bu değerler yalnızca
yerel değerlendirme içindir; bunları yeniden kullanmayın veya production ortamında `.env`
dosyasına gerçek secret'lar commit etmeyin.**

İlk başlatmada Admin API, case'in demo veri setini (`SiteName`, `IsBasketEnabled`,
`MaxItemCount`) idempotent şekilde seed'ler. Veri `mongo-data` volume'ünde tutulur; bu nedenle
`docker compose down` / `up` sonrasında da kalıcıdır.

| Servis | URL (host) | Notlar |
|---|---|---|
| Admin API (`config-api`) | http://localhost:8080 | `X-Api-Key` header'ı gerektirir. Swagger UI yalnızca Development ortamında. |
| Admin UI (`config-ui`) | http://localhost:8081 | Admin key `.env`'den otomatik okunur; isim filtresi client-side çalışır. |
| `mongo` / `redis` | dışarı açık değil | Yalnızca dahili compose ağında erişilebilir. |

Bir değeri güncelleyin ve bir tüketicinin bunu yakaladığını izleyin:

```bash
# Kayıtları listele (SiteName id'sini bulmak için):
curl -s -H "X-Api-Key: $ADMIN_API_KEY" http://localhost:8080/api/configurations

# SERVICE-A'nın SiteName değerini güncelle:
curl -X PUT -H "X-Api-Key: $ADMIN_API_KEY" -H "Content-Type: application/json" \
  -d '{"name":"SiteName","type":"string","value":"updated.soty.io","isActive":true,"applicationName":"SERVICE-A"}' \
  http://localhost:8080/api/configurations/<id>

# SERVICE-A'nın yeni değeri yansıttığını gözlemle (Redis broker sayesinde anında):
docker compose logs -f service-a

# Temizle (mongo volume'ünü de silmek için -v ekleyin):
docker compose down
```

---

## Case isterleri nasıl karşılanıyor

| İster | Nerede |
|---|---|
| En fazla 3 constructor parametresi; `T GetValue<T>(key)` | `ConfigurationReader` public yüzeyi. |
| `GetValue<string>("SiteName") == "soty.io"` | Seed edilen veri; `service-a` loglarında canlı olarak gösterilir. |
| Yalnızca `IsActive = 1` kayıtlar döner | `MongoConfigurationStore.GetActiveRecordsAsync` içinde server-side filtre. `MaxItemCount` (inactive) `service-a`'da `<absent>` olarak kalır. |
| Uygulama başına izolasyon (tenant'lar arası sızıntı yok) | `ApplicationName`'e göre filtrelenir; `service-b`, `service-a`'nın `SiteName`'ini asla göremez. |
| Storage erişilemez olduğunda çalışabilme (fallback) | Son başarılı snapshot korunur; hatalar loglanır, tüketiciye asla fırlatılmaz. |
| Periyodik refresh, restart gerektirmez | `PeriodicTimer` döngüsü + atomik snapshot değişimi (concurrency-safe cache). |
| **Ekstra:** anlık güncellemeler için message broker | Redis Pub/Sub (`ConfigReader.Broker.Redis`); polling birincil garanti olmaya devam eder. |
| **Ekstra:** async/await & TPL | `PeriodicTimer`, `IAsyncDisposable`, baştan sona async store/broker çağrıları. |
| **Ekstra:** TDD | Tüm katmanlarda xUnit + FluentAssertions + NSubstitute + Testcontainers. |
| **Ekstra:** çalışır ekosistem | `docker-compose.yml` (mongo, redis, config-api, config-ui, service-a, service-b). |
| **Ekstra:** dokümantasyon | Bu README. |

---

## Güvenlik ve operasyon (Admin API)

### TLS / HTTPS (production isteri)

Yerel `docker-compose` ortamında Admin API kolaylık amacıyla düz HTTP üzerinden erişilir.
**Production'da HTTPS zorunludur** — API, her servisin canlı davranışını değiştirebilen bir admin
API key taşır; bu nedenle transport uçtan uca şifrelenmelidir.

Bunu iki standart yoldan biriyle etkinleştirin:

- **Reverse proxy / ingress termination (önerilen):** TLS'i nginx / Traefik / bir cloud load
  balancer'da sonlandırın ve dahili ağ üzerinden API'ye yönlendirin. Uygulamanın orijinal şemayı
  görmesi için `ForwardedHeaders`'ı yapılandırın.
- **Kestrel direct TLS:** `ASPNETCORE_Kestrel__Certificates__Default__Path` ve `...__Password`
  (veya `ASPNETCORE_URLS=https://+:443`) üzerinden bir sertifika sağlayın ve
  `UseHttpsRedirection`'ı aktif tutun.

Admin API'yi güvenilir bir yerel ağın dışında asla düz HTTP üzerinde açığa çıkarmayın.

### Kimlik doğrulama (CFG-9.1)

Tüm endpoint'ler (okumalar dahil) geçerli bir `X-Api-Key` header'ı gerektirir. Key,
konfigürasyon/ortam değişkeninden (`AdminApi__ApiKey`) okunur — asla koda gömülmez — ve
ayarlanmamışsa API kapalı olarak başarısız olur (401).

### CORS (CFG-9.6)

API'yi yalnızca `AdminApi:AllowedOrigins` listesindeki tarayıcı origin'leri çağırabilir.
`AllowAnyOrigin` hiçbir zaman kullanılmaz.

### Rate limiting (CFG-9.6)

Yazma endpoint'leri (`POST`/`PUT`) sabit pencereli (fixed-window) bir limiter ile kısıtlanır.
`AdminApi:WriteRateLimit:PermitLimit` ve `AdminApi:WriteRateLimit:WindowSeconds` ile ayarlayın.

### Hata yanıtları (CFG-9.6)

Yakalanmamış exception'lar, bir `correlationId` içeren genel bir `ProblemDetails` döndürür; stack
trace'ler, iç mesajlar ve connection string'ler yalnızca dahili olarak loglanır, hiçbir zaman
client'a gönderilmez. Her yanıta temel güvenlik header'ları (`X-Content-Type-Options: nosniff`,
`X-Frame-Options: DENY`) eklenir.
</content>
