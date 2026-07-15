# Test Stratejisi

Bu çözüm TDD ile geliştirilir. Her davranışsal görev (CFG-2.x / 3.x / 4.x / 5.x) kendi
testleriyle birlikte gelir; bu belge test altyapısının nasıl organize edildiğini ve hangi
senaryonun hangi kategoride koşacağını tanımlar.

## Araçlar

| Araç | Amaç |
| --- | --- |
| **xUnit** | Test runner + assertion çatısı |
| **FluentAssertions** | Okunur, niyet-anlatan assertion'lar (`result.Should().Be(...)`) |
| **NSubstitute** | Port arayüzlerini (ör. `IConfigurationStore`, `IChangeNotifier`) mock/stub'lama |
| **Testcontainers** | Entegrasyon testlerinde gerçek MongoDB / Redis container'ı ayağa kaldırma |

## Unit vs. Entegrasyon Ayrımı

Her test projesinde iki alt klasör bulunur:

- **`UnitTests/`** — Saf mantık; hiçbir dış bağımlılık (Mongo, Redis, HTTP) gerektirmez.
  Bağımlılıklar NSubstitute ile mock'lanır. Hızlı ve deterministiktir; her build'de koşar.
- **`IntegrationTests/`** — Gerçek altyapı gerektiren senaryolar. Testcontainers ile gerçek bir
  MongoDB / Redis container'ı başlatılır, senaryo koşar, container kapatılır. Docker gerektirir.

## Projeler ve Sorumlulukları

| Test Projesi | Unit Testler | Entegrasyon Testleri (Testcontainers) |
| --- | --- | --- |
| **ConfigReader.Core.Tests** | Tip dönüşümü, cache snapshot, `ApplicationName` izolasyonu, fallback davranışı (store NSubstitute ile mock'lanır) | Yok — Core hiçbir somut altyapıya bağlı değildir (klasör bilinçli olarak boş kalır) |
| **ConfigReader.Storage.Mongo.Tests** | Mapping / dönüşüm mantığı (gerekirse mock ile) | `Testcontainers.MongoDb` ile gerçek Mongo'ya karşı `IConfigurationStore` sorguları (`IsActive`, `ApplicationName` filtresi) — CFG-2.2 |
| **ConfigReader.Broker.Redis.Tests** | Notifier sözleşme mantığı (mock ile) | `Testcontainers.Redis` ile gerçek Redis'e karşı pub/sub yayın/abonelik — CFG-3.x |
| **ConfigReader.Admin.Api.Tests** | Use-case servis validasyonu, DTO map (port'lar NSubstitute ile mock'lanır) | `WebApplicationFactory` + Testcontainers.MongoDb ile uçtan uca CRUD / geçersiz Type-Value → 400 |

## Testcontainers Kararı

Entegrasyon testleri sahte (in-memory) storage yerine **gerçek Mongo/Redis container'ı** kullanır;
böylece sürücü davranışı, sorgu filtreleri ve pub/sub semantiği prod'a en yakın şekilde doğrulanır.
Container yaşam döngüsü test sınıfının kurulum/temizlik (fixture) adımlarında yönetilir. Bu testler
Docker'ın kurulu ve çalışır olmasını gerektirir; bu yüzden hızlı unit testlerden ayrı klasörde tutulur.

> Not: Gerçek container'lı entegrasyon testleri ilgili implementasyon görevlerinde
> (CFG-2.2 Mongo, CFG-3.x Redis) yazılır. Bu görev (CFG-7.1) yalnızca altyapıyı — paket
> referanslarını, klasör desenini ve stratejiyi — kurar.
