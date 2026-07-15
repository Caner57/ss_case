# `configurations` Koleksiyon Şeması ve Index Stratejisi (CFG-2.1)

Bu belge, MongoDB storage adapter'ının (`ConfigReader.Storage.Mongo`) dayandığı
`configurations` koleksiyonunun belge şemasını, alan tiplerini ve index stratejisini
tanımlar. CFG-2.2 (CRUD + filtreleme) ve CFG-2.3 (seed) bu şemayı bağlayıcı temel
olarak kullanır; alan isimleri ve index kararları sonraki görevlerde değişmez.

## 1. Belge Şeması

Koleksiyon: **`configurations`** (veritabanı: `configdb`).

Her belge, case'deki kayıt tablosunun 6 alanını birebir karşılar. Domain modeli
`ConfigReader.Core.Domain.ConfigurationRecord` ile alan eşlemesi:

| Case alanı        | Belge alanı (BSON) | BSON tipi | Domain alanı (`ConfigurationRecord`) | Not |
|-------------------|--------------------|-----------|--------------------------------------|-----|
| `Id`              | `_id`              | ObjectId  | `Id` (string temsili)                | Mongo birincil anahtarı; string olarak taşınır |
| `Name`            | `name`             | string    | `Name`                               | Konfigürasyon anahtarı (ör. `SiteName`) |
| `Type`            | `type`             | string    | `Type`                               | Tip token'ı: `string`/`int`/`integer`/`double`/`bool`/`boolean` |
| `Value`           | `value`            | string    | `Value`                              | **Her zaman string saklanır**; `Type`'a göre çalışma anında dönüştürülür |
| `IsActive`        | `isActive`         | bool      | `IsActive`                           | Yalnızca `true` olan kayıtlar kütüphane sorgusunda döner |
| `ApplicationName` | `applicationName`  | string    | `ApplicationName`                    | **İzolasyonun temeli** — her sorguda zorunlu filtre |

### `Value` string, `Type` token kararı

`Value` alanı, konfigürasyonun tipi ne olursa olsun (`int`, `double`, `bool`, `string`)
storage'da **her zaman string** olarak tutulur (ör. `"50"`, `"true"`, `"soty.io"`).
Gerçek .NET tipine dönüşüm okuma anında, `Type` token'ına bakılarak yapılır (bkz.
`ConfigurationTypeResolver` — case-insensitive ve varyant-toleranslı: `int`/`integer`,
`bool`/`boolean`). Bu karar CFG-1.2 ile tutarlıdır ve şema seviyesinde teyit edilir:
koleksiyon `value` alanında karışık BSON tipleri tutmaz; tek tip (string) tutar, böylece
şema esnekliği ve deterministik dönüşüm birlikte sağlanır.

## 2. Örnek Belgeler (case'deki 3 kayıt)

Case tablosundaki 3 örnek satır bu şemaya somut belgeler olarak şöyle oturur:

```json
{ "_id": ObjectId("..."), "name": "SiteName",        "type": "string", "value": "soty.io", "isActive": true,  "applicationName": "SERVICE-A" }
{ "_id": ObjectId("..."), "name": "IsBasketEnabled", "type": "bool",   "value": "1",       "isActive": true,  "applicationName": "SERVICE-B" }
{ "_id": ObjectId("..."), "name": "MaxItemCount",    "type": "int",    "value": "50",      "isActive": false, "applicationName": "SERVICE-A" }
```

Dikkat: `MaxItemCount` kaydı `isActive: false`'tur (case tablosuna sadık). Bu sayede
kütüphanenin aktiflik filtresinin bu kaydı elemesi CFG-3.x'te doğrulanabilir.

## 3. Index Stratejisi

### 3.1 Birincil index — `(applicationName, isActive)` bileşik index

```
db.configurations.createIndex({ applicationName: 1, isActive: 1 })
```

- **Neden:** Kütüphanenin **tek ve birincil erişim paterni**, "belirli bir
  `ApplicationName` için tüm `IsActive=1` kayıtlar" sorgusudur
  (`IConfigurationStore.GetActiveRecordsAsync`). Bu sorgu her zaman tam olarak bu iki
  alanla filtreler.
- **Ne kazandırır:** Sorgu bir **collection scan** yerine bu bileşik index üzerinden
  bir index seek yapar. Alan sırası (`applicationName` önce, `isActive` sonra) eşitlik
  filtreleri için idealdir: en seçici/zorunlu ayrım (`applicationName`) prefix'te olduğu
  için, aynı index tek bir `applicationName` değerini de (Admin API listelemesi)
  hızlandırabilir (prefix kullanımı).
- **Senaryo karşılığı:** `applicationName="SERVICE-A"` + `isActive=true` sorgusu bu
  index'i kullanır; collection scan olmaz.

### 3.2 Benzersizlik index'i — `(applicationName, name)` unique

```
db.configurations.createIndex({ applicationName: 1, name: 1 }, { unique: true })
```

- **Neden:** Bir uygulama içinde aynı `Name` iki kez bulunmamalıdır (bir anahtarın tek
  bir tanımı olur). Aynı `Name` **farklı** `ApplicationName` altında serbestçe var olabilir
  (ör. `SiteName` hem SERVICE-A hem SERVICE-B'de) — unique index bileşik olduğu için bu
  çokluğu destekler, yalnızca aynı uygulama içindeki tekrarı engeller.
- **Yan fayda:** CFG-2.3 seed idempotency'si için doğal koruma sağlar; aynı kayıt iki kez
  eklenmeye çalışılırsa duplicate-key ile reddedilir.

## 4. İzolasyonun Storage Temeli

`applicationName`, çoklu-uygulama (multi-tenant) izolasyonunun **storage seviyesindeki
temelidir**. Kütüphane erişimi için yazılan her sorgu, `applicationName`'i **zorunlu**
filtre olarak içerir; hiçbir sorgu "tüm kayıtları çek" yapmaz. Böylece bir uygulama
başka bir uygulamanın kaydını hiçbir koşulda göremez (cross-tenant sızıntı yok — case:
"Her servis yalnızca kendi konfigürasyon kayıtlarına erişebilmeli"). Filtre daima
server-side ve MongoDB driver'ın **tipli filtre builder**'ı ile kurulur (string
birleştirme/interpolasyon yok — NoSQL injection riski elenir). Bu garanti CFG-2.2'de
kod seviyesinde implemente ve entegrasyon testiyle kanıtlanır.

## 5. Sonraki Görevler İçin Bağlayıcılık

- **CFG-2.2:** `IConfigurationStore` implementasyonu bu koleksiyon adını, alan
  eşlemesini ve `(applicationName, isActive)` index'ini varsayar; index'i başlatırken
  (`EnsureIndexes`) buradaki tanımları kullanır.
- **CFG-2.3:** Seed, örnek 3 kaydı bu şemaya birebir uygun belgeler olarak ekler ve
  `(applicationName, name)` unique index'i ile idempotency'i pekiştirir.

## 6. Seed ve Migration Stratejisi (CFG-2.3)

### 6.1 İlk açılış seed'i (idempotent)

Uygulama ilk açıldığında `MongoConfigurationSeeder.SeedAsync(CaseSeedData.Records)`
çağrılır. Seeder yalnızca **koleksiyon tamamen boşsa** (`CountDocuments == 0`) case'deki
3 kaydı ekler; koleksiyonda tek bir belge bile varsa seed atlanır (mevcut veri ezilmez,
tekrar eklenmez). Böylece uygulama defalarca yeniden başlasa da kayıtlar tekrarlanmaz.
Seed edilecek 3 kayıt tek bir yerde (`ConfigReader.Core.Domain.CaseSeedData`) case
tablosuyla birebir tanımlıdır: `SiteName/string/soty.io/aktif/SERVICE-A`,
`IsBasketEnabled/bool/1/aktif/SERVICE-B`, `MaxItemCount/int/50/pasif/SERVICE-A`.

### 6.2 Şema versiyonlama / migration yaklaşımı

Şema ileride değişirse (alan ekleme/yeniden adlandırma, tip dönüşümü) izlenecek yol:

- **Versiyon işaretleyici:** `configurations` belgelerine (veya ayrı bir
  `schema_migrations` koleksiyonuna) bir `schemaVersion` alanı eklenir. Uygulama açılışta
  mevcut sürümü okur; hedef sürümden düşükse ilgili migration adımını çalıştırır.
- **İleriye dönük uyumluluk:** Yeni alanlar opsiyonel/varsayılan değerli eklenir; okuma
  yolu eksik alanı tolere eder (belge tabanlı esneklik). Böylece migration çevrimiçi
  yapılabilir, restart/deploy zorunluluğu doğmaz.
- **İdempotent ve ileri-yönlü:** Her migration adımı yeniden çalıştırılabilir (idempotent)
  ve yalnızca ileri yönde uygulanır; geri alma gerekiyorsa yeni bir sürüm adımı yazılır.
- **Bu ölçekte:** Case kapsamı için tam bir migration çatısı gereksizdir; boot-time
  idempotent seed + yukarıdaki `schemaVersion` konvansiyonu, gelecekteki şema değişimi
  için izlenecek yolu tanımlar (uygulama gerçekte ihtiyaç doğduğunda eklenir).
