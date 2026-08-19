# Denetim Panosu — Blazor Server / DevExpress v25.2 kurulumu

Dosyalar
- `DenetimDashboard.razor` — sayfa bileşeni (`/denetim-dashboard`)
- `DenetimDashboard.razor.css` — CSS isolation (Power BI renk paleti)
- `DashboardModels.cs` — modeller, renk sabitleri, squarified treemap algoritması, örnek veri kaynağı

## 1. Paket ve servisler
```bash
dotnet add package DevExpress.Blazor --version 25.2.*
```
`Program.cs`:
```csharp
builder.Services.AddDevExpressBlazor(o => o.BootstrapVersion = BootstrapVersion.v5);
builder.Services.AddScoped<Denetim.Dashboard.DenetimRepository>();
```
`App.razor` / `_Host.cshtml` içinde bir DevExpress teması:
```html
<link href="_content/DevExpress.Blazor.Themes/blazing-berry.bs5.min.css" rel="stylesheet" />
```

## 2. _Imports.razor
```razor
@using DevExpress.Blazor
@using Denetim.Dashboard
```

## 3. Fontlar
Archivo (Google Fonts) veya kurum fontunuz:
```html
<link href="https://fonts.googleapis.com/css2?family=Archivo:wght@400;600;700;800&display=swap" rel="stylesheet" />
```

## 4. TreeMap
DevExpress Blazor'da TreeMap bileşeni yok. İki hazır sarmalayıcı eklendi, parametreleri aynı (`Data`, `Selected`, `OnCellClick`) — birini diğeriyle değiştirmek tek satır:

**Seçenek 2 — Blazor-ApexCharts (varsayılan, MIT):** `ApexTreeMap.razor`
```bash
dotnet add package Blazor-ApexCharts
```
`_Imports.razor`: `@using ApexCharts` · `App.razor`: `<script src="_content/Blazor-ApexCharts/js/apex-charts.min.js"></script>`

**Seçenek 1 — DevExtreme dxTreeMap (JS interop):** `DevExtremeTreeMap.razor` + `wwwroot/js/treemap-interop.js`
```html
<script src="_content/DevExpress.Blazor/dx.all.js"></script>
```
(veya `https://cdn3.devexpress.com/jslib/25.2.x/js/dx.all.js`). Kullanmak için `DenetimDashboard.razor` içindeki `TreemapVisual` parçasında `<ApexTreeMap ...>` yerine `<DevExtremeTreeMap ...>` yazın.

**Seçenek 3 — bağımlılıksız:** `DashboardModels.cs` içindeki `Treemap.Build()` squarify algoritması + `.rp-treemap-cell` CSS sınıfları hâlâ duruyor; hiç paket eklemek istemezseniz o parçayı geri koyabilirsiniz.

## 5. Diğer notlar
- **Grafikler**: `DxChart` (bar / stacked bar / line), `DxPieChart` (pie & donut), tablolar `DxGrid` (sanal kaydırma + toplam satırı).
- **Filtreler**: bileşen state'inde tutulur. Kalıcı ve sayfalar arası paylaşımlı olması için filtreleri bir `ReportFilterState` servisine taşıyıp `CascadingValue` ile dağıtın; sayfa/filtre durumunu `NavigationManager` ile QueryString'e yazarsanız link paylaşımı da çalışır.
- **Veri**: `DenetimRepository` örnek verilerle sabittir. Gerçekte Power BI ile aynı veri ambarına bağlanıp (EF Core view / stored procedure) aynı ölçüleri döndüren bir servisle değiştirin.
- **Yerelleştirme**: Sayı biçimleri `tr-TR` kültürüne göre; `Program.cs` içinde `CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("tr-TR");` ayarlayın.
