# Denetim Panosu — Gerçek Veriye Geçiş Rehberi

.NET 10 · Blazor Server · DevExpress Blazor v25.2 · SQL Server

---

## 1. Neden "repository'yi doldur, bitti" demiyoruz

Şu anki `DenetimRepository` bellekte sabit listeler tutuyor ve bileşen bunlara doğrudan
erişiyor. Bunu olduğu gibi EF Core'a çevirirseniz iki şey birden bozulur:

1. **Blazor Server'da scoped `DbContext` = devre (circuit) ömrü.** Kullanıcı sekmeyi
   saatlerce açık bıraktığında aynı `DbContext` yaşamaya devam eder; change tracker büyür,
   veri bayatlar ve iki render aynı anda tetiklenirse `DbContext` thread-safe olmadığı için
   `InvalidOperationException` alırsınız. Bu, Blazor Server'ın en sık görülen üretim hatası.
2. **Her render'da sorgu.** Eski koddaki `FilteredSubeler`, `Kpis` gibi computed
   property'ler her render'da yeniden değerlendiriliyordu. `IQueryable` ile aynı şeyi
   yaparsanız her tuş vuruşunda SQL'e gidersiniz. (Bu yüzden razor dosyasında hepsini
   `LoadAsync()` içinde önbelleğe aldım.)

Aşağıdaki yapı bu ikisini baştan engelliyor.

---

## 2. Katmanlar

```
Pages/Dashboards/DenetimDashboard.razor      → sadece görünüm + filtre state'i
  ↓ inject
IDenetimDashboardService                     → uygulama servisi (arayüz)
  ↓
DenetimDashboardService                      → IDbContextFactory<DenetimDbContext> + HybridCache
  ↓
SQL Server: vw_* görünümleri / sp_* prosedürleri
```

Razor dosyasında yalnızca `LoadAsync()` değişir. Markup'a hiç dokunmazsınız.

---

## 3. Filtreleri tek bir değer nesnesine alın

```csharp
public sealed record DashboardFilter(
    DashboardPage Page,
    IReadOnlySet<string> PlanYears,
    IReadOnlySet<RiskLevel> RiskLevels,
    string? Portfoy,
    string? DenetimTuru,
    string? Search)
{
    /// Önbellek anahtarı — record eşitliği sayesinde deterministik.
    public string CacheKey =>
        $"dash:{Page}:{string.Join(',', PlanYears.Order())}:" +
        $"{string.Join(',', RiskLevels.Order())}:{Portfoy}:{DenetimTuru}:{Search}";
}
```

Bunun üç faydası var: servis imzası tek parametreye iner, `HybridCache` anahtarı bedava
gelir, ve aynı string'i `NavigationManager` ile query string'e yazarak **paylaşılabilir
rapor linki** (Power BI'daki gibi) elde edersiniz.

---

## 4. DbContext: mutlaka factory

`Program.cs`:

```csharp
builder.Services.AddDbContextFactory<DenetimDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Denetim"),
        sql => sql.CommandTimeout(30))
     .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.AddScoped<IDenetimDashboardService, DenetimDashboardService>();
builder.Services.AddHybridCache();   // .NET 9+ / 10
```

Servis içinde her sorgu için kısa ömürlü context:

```csharp
public sealed class DenetimDashboardService(
    IDbContextFactory<DenetimDbContext> factory,
    HybridCache cache) : IDenetimDashboardService
{
    public async Task<GenelBakisVm> GetGenelBakisAsync(DashboardFilter f, CancellationToken ct)
        => await cache.GetOrCreateAsync(
            $"genelbakis:{f.CacheKey}",
            async token =>
            {
                await using var db = await factory.CreateDbContextAsync(token);
                // ... sorgular
            },
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(10) },
            cancellationToken: ct);
}
```

`AddDbContextFactory` + `await using` = devre ömrü boyunca yaşayan context sorunu yok.

---

## 5. Agregaları C#'ta değil SQL'de yapın

Power BI'daki her ölçü (measure) bir SQL agregasına karşılık gelmeli. Bulguları çekip
`.Sum()` yapmak yerine:

```sql
CREATE OR ALTER VIEW vw_PlanYiliBulguSeviyesi AS
SELECT  d.PlanYili                                  AS Label,
        SUM(CASE WHEN b.RiskSeviyesi = 1 THEN 1 ELSE 0 END) AS Dusuk,
        SUM(CASE WHEN b.RiskSeviyesi = 2 THEN 1 ELSE 0 END) AS Orta,
        SUM(CASE WHEN b.RiskSeviyesi = 3 THEN 1 ELSE 0 END) AS Yuksek
FROM    Denetim d
JOIN    Bulgu   b ON b.DenetimId = d.Id
GROUP BY d.PlanYili;
```

EF Core tarafında **keyless entity** olarak eşleyin — okuma amaçlı, tracking yok:

```csharp
protected override void OnModelCreating(ModelBuilder mb)
{
    mb.Entity<StackedRow>().HasNoKey().ToView("vw_PlanYiliBulguSeviyesi");
    mb.Entity<SektorRow>().HasNoKey().ToView("vw_SektorPaydaslik");
}
```

Karmaşık, çok parametreli ölçüler için stored procedure + `SqlQuery<T>` daha okunur olur:

```csharp
var rows = await db.Database
    .SqlQuery<StackedRow>($"EXEC sp_PlanYiliBulgu @PlanYillari = {yearsCsv}")
    .ToListAsync(ct);
```

**Kural:** panoda gösterilen her sayı ya bir view/SP'den gelir ya da `TODO(db)` işaretli
kalır. Razor dosyasındaki `bulguToplam * 1.34`, `"96"`, `!= 201` gibi türetilmiş değerlerin
hepsini bu şekilde işaretledim; hiçbiri sessizce yanlış sayı üretmesin.

---

## 6. Sayfa başına tek gidiş-dönüş

Altı ayrı `await` yerine, aktif sayfanın tüm görsellerini tek DTO'da döndürün:

```csharp
public sealed record GenelBakisVm(
    List<StackedRow>    PlanRows,
    List<CategoryValue> Portfoy,
    List<CategoryValue> RaporGenelGorusu,
    List<SektorRow>     Sektorler,
    List<KpiItem>       Kpis);
```

Servis içinde paralel çalıştırabilirsiniz — **ama her `Task` kendi context'ini
oluşturmalı**, aynı `DbContext`'i paylaşan paralel sorgular patlar:

```csharp
var planTask    = QueryAsync(db => db.Set<StackedRow>().Where(...).ToListAsync(ct));
var sektorTask  = QueryAsync(db => db.Set<SektorRow>().ToListAsync(ct));
await Task.WhenAll(planTask, sektorTask);

async Task<T> QueryAsync<T>(Func<DenetimDbContext, Task<T>> q)
{
    await using var db = await factory.CreateDbContextAsync(ct);
    return await q(db);
}
```

Sonra `LoadAsync()` şuna dönüşür:

```csharp
async Task LoadAsync()
{
    cts?.Cancel(); cts?.Dispose();
    cts = new CancellationTokenSource();
    var ct = cts.Token;

    loading = true;
    try
    {
        var vm = await Service.GetGenelBakisAsync(CurrentFilter, ct);
        if (ct.IsCancellationRequested) return;
        planRows = vm.PlanRows; kpis = vm.Kpis; /* ... */
        lastRefresh = DateTime.Now;
    }
    catch (OperationCanceledException) { return; }   // filtre yine değişti, sorun değil
    finally { loading = false; }

    await InvokeAsync(StateHasChanged);
}
```

`CancellationTokenSource` iskeletini razor dosyasına şimdiden koydum — hızlı filtre
değiştirmede uçuşan sorgular iptal edilir, "eski sonuç yeniyi eziyor" yarış durumu olmaz.
Yükleme sırasında `DxLoadingPanel` veya kartlarda iskelet göstermeyi ekleyin.

---

## 7. Önbellek: denetim verisi yavaş değişir

Denetim bulguları saniyede değişmez. `HybridCache` ile 5–15 dakikalık TTL, veritabanı
yükünü büyük ölçüde düşürür ve dilimleyiciler anında tepki verir. Filtre kombinasyonu
sayısı yönetilebilir (yıl × portföy × risk) olduğu için anahtar patlaması olmaz.

Üstteki "Son yenileme" etiketini önbellek zaman damgasıyla besleyin; kullanıcı verinin ne
kadar taze olduğunu görmeli — denetim ekibi için bu bir güven meselesi.

---

## 8. DxGrid ve büyük tablolar

- **Şubeler (~100–2.000 satır):** filtrelenmiş `List<T>` bağlayın. Şu anki hâli budur ve
  doğrudur. Yeni bir `IEnumerable` referansı vermek grid'i sıfırdan yükletir; bu yüzden
  listeyi yalnız `LoadAsync()` içinde üretiyoruz, her render'da değil.
- **Bulgu/aksiyon detayı (on binlerce satır):** grid'in sıralama/filtreleme/sayfalamayı
  SQL'e çevirmesi için `IQueryable` bağlamak gerekir. Burada dikkat: `IQueryable`
  yaşadığı sürece `DbContext`'in de yaşaması gerekir, yani `await using` deseni
  çalışmaz — o ekran için context'i bileşen ömrüne bağlayıp `IAsyncDisposable` ile
  kapatın, ya da DevExpress'in uzak veri kaynağı sarmalayıcısını kullanın.
  v25.2'deki güncel tip adını IntelliSense'ten doğrulayın (bu oturumda dokümana
  erişemedim, yanlış isim vermek istemem).
- Sunucu tarafı işlem yapan grid'lerde `VirtualScrollingEnabled` yerine sayfalama
  genelde daha öngörülebilir davranır.

---

## 9. Arama: `StringComparison.CurrentCultureIgnoreCase` EF Core'da çalışmaz

Mevcut kod bellekte doğru çalışıyor ama `IQueryable`'a taşırsanız EF Core bunu SQL'e
çeviremez — ya patlar ya da tüm tabloyu çekip istemcide filtreler. Türkçe için:

```csharp
q = q.Where(s => EF.Functions.Like(s.SubeAdi, $"%{search}%"));
```

Büyük/küçük harf ve i/ı duyarlılığını **kolasyon** belirler. Sütun kolasyonunuz
`Turkish_CI_AI` (case-insensitive, accent-insensitive) ise `LIKE` beklediğiniz gibi
davranır. Değilse:

```csharp
mb.Entity<SubeRow>().Property(p => p.SubeAdi).UseCollation("Turkish_CI_AI");
```

Ayrıca `Program.cs`'te kültürü sabitleyin, yoksa `"N0"` biçimi ve tarihler sunucu
kültürüne göre değişir:

```csharp
var tr = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = tr;
CultureInfo.DefaultThreadCurrentUICulture = tr;
```

---

## 10. Yetkilendirme — denetim verisinde bu isteğe bağlı değil

Bir denetçi yalnız yetkili olduğu portföyün/şubenin bulgularını görmeli. Bunu **servis
katmanında**, kullanıcının kimliğinden türetilen bir filtreyle yapın:

```csharp
var yetkiliPortfoyler = await authz.GetPortfoylerAsync(user, ct);
q = q.Where(x => yetkiliPortfoyler.Contains(x.Portfoy));
```

UI filtresine güvenmeyin — dilimleyiciler kullanıcı tercihi, yetki değil. Sayfaya
`@attribute [Authorize(Policy = "DenetimGoruntule")]` ekleyin. EF Core global query
filter (`HasQueryFilter`) bunu unutma riskini azaltır.

---

## 11. Geçiş sırası (önerilen)

1. `IDenetimRepository` arayüzünü çıkarın, mevcut `DenetimRepository` onu uygulasın.
   Razor'da `@inject IDenetimRepository` yapın → derleme bozulmaz, davranış aynı.
2. Bir görsel seçin (ör. Sektörler tablosu). `vw_SektorPaydaslik` view'ını yazın,
   keyless entity ile eşleyin, `SqlDenetimRepository`'de yalnız o metodu gerçek veriye
   bağlayın. Diğerleri hâlâ sabit veriden gelir.
3. Sayı Power BI raporundakiyle **birebir aynı** çıkana kadar bırakmayın. Bu adımı her
   görsel için tekrarlayın — Power BI raporu sizin regresyon testiniz.
4. Tüm `TODO(db)` işaretleri kalkınca `DenetimRepository`'yi ve `DashboardModels.cs`
   içindeki ölü kodu (`Treemap.Build`, `TreemapCell` — artık ApexCharts/DevExtreme
   yerleşimi yapıyor) silin.
5. `CategoryValue` içindeki `Color` alanını veri modelinden çıkarıp bir UI eşleyicisine
   taşıyın. Renk sunum bilgisidir; SQL'den renk kodu gelmemeli.

---

## 12. Sonraki adımda büyüyecekse

Tek `.razor` dosyası şu an 6 sayfayı taşıyor. Her filtre değişiminde **aktif olmayan
sayfaların markup'ı da** diff'e giriyor. Sayfa sayısı artarsa her sayfayı kendi bileşenine
ayırın (`GenelBakisPage.razor`, `SubelerPage.razor` …) ve filtreleri `CascadingValue` ile
geçirin. Bu hem CS0841 sınıfı kapsam sorunlarını tamamen ortadan kaldırır hem de
DevExpress bileşenlerinin gereksiz yeniden render'ını `ShouldRender` ile kesmenizi sağlar.
