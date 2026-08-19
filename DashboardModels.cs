using System;
using System.Collections.Generic;
using System.Linq;

namespace Denetim.Dashboard;

public enum RiskLevel { Dusuk, Orta, Yuksek }

public record KpiItem(string Label, string Value, string? Delta = null);
public record CategoryValue(string Label, double Value, string Color);
public record StackedRow(string Label, int Dusuk, int Orta, int Yuksek)
{
    public int Total => Dusuk + Orta + Yuksek;
}
public record SektorRow(string Sektor, int Rapor, int Bulgu, int Aksiyon);
public record SubeRow(string DenetimTuru, string SubeAdi, RiskLevel Risk);
public record ProjectionPoint(string Ay, int Plan, int? Gercek);

/// <summary>Rapor sayfaları — Power BI'daki sayfa sekmelerinin karşılığı.</summary>
public enum DashboardPage
{
    Anasayfa,
    GenelBakis,
    BulgularAksiyonlar,
    AksiyonProjeksiyon,
    BulguProjeksiyon,
    Subeler
}

/// <summary>Rapor renk paleti (Power BI temасından).</summary>
public static class ReportColors
{
    public const string Navy = "#0e2a4d";
    public const string Blue = "#2222c8";
    public const string LightBlue = "#2196c9";
    public const string Orange = "#e8853c";
    public const string Teal = "#12a3a0";
    public const string Red = "#c00000";
    public const string Green = "#79b93c";
    public const string Yellow = "#f2c811";
    public const string Purple = "#7b2382";
    public const string Magenta = "#d43ba8";
    public const string Gray = "#d9d9d9";

    public static string[] Categorical => new[] { Blue, Orange, Magenta, Purple, LightBlue, Teal };

    public static string ForRisk(RiskLevel r) => r switch
    {
        RiskLevel.Yuksek => Red,
        RiskLevel.Orta => Orange,
        _ => Teal
    };
}

/// <summary>Treemap dikdörtgeni (yüzde koordinat).</summary>
public record TreemapCell(string Label, double Value, double X, double Y, double W, double H, string Color);

public static class Treemap
{
    /// <summary>Squarified treemap (Bruls et al.) — 0..100 yüzde koordinatlarında hücreler döner.</summary>
    public static List<TreemapCell> Build(IEnumerable<CategoryValue> items)
    {
        var data = items.Where(i => i.Value > 0).OrderByDescending(i => i.Value).ToList();
        var result = new List<TreemapCell>();
        if (data.Count == 0) return result;

        double total = data.Sum(i => i.Value);
        double scale = 100d * 100d / total;
        double x = 0, y = 0, w = 100, h = 100;
        int i = 0;

        static double Worst(List<double> row, double side)
        {
            double sum = row.Sum(), max = row.Max(), min = row.Min();
            double s2 = sum * sum, side2 = side * side;
            return Math.Max(side2 * max / s2, s2 / (side2 * min));
        }

        while (i < data.Count)
        {
            double side = Math.Min(w, h);
            var row = new List<double> { data[i].Value * scale };
            int n = 1;
            while (i + n < data.Count)
            {
                double next = data[i + n].Value * scale;
                var candidate = new List<double>(row) { next };
                if (Worst(candidate, side) <= Worst(row, side)) { row.Add(next); n++; }
                else break;
            }

            double sum = row.Sum();
            if (w >= h)
            {
                double cw = sum / h, cy = y;
                for (int k = 0; k < row.Count; k++)
                {
                    double ch = row[k] / cw;
                    result.Add(new TreemapCell(data[i + k].Label, data[i + k].Value, x, cy, cw, ch, data[i + k].Color));
                    cy += ch;
                }
                x += cw; w -= cw;
            }
            else
            {
                double ch = sum / w, cx = x;
                for (int k = 0; k < row.Count; k++)
                {
                    double cw2 = row[k] / ch;
                    result.Add(new TreemapCell(data[i + k].Label, data[i + k].Value, cx, y, cw2, ch, data[i + k].Color));
                    cx += cw2;
                }
                y += ch; h -= ch;
            }
            i += row.Count;
        }
        return result;
    }
}

/// <summary>
/// Örnek veri kaynağı. Gerçek uygulamada bu sınıfı bir servise (EF Core / SQL / API) bağlayın:
/// builder.Services.AddScoped&lt;IDenetimRepository, SqlDenetimRepository&gt;();
/// </summary>
public class DenetimRepository
{
    public List<SektorRow> Sektorler { get; } = new()
    {
        new("BT Dışı - Bankacılık Servis Grubu", 9, 35, 53),
        new("Dijital Bankacılık ve Ödeme Sistemleri", 8, 31, 32),
        new("BT - Bankacılık Servis Grubu", 7, 22, 24),
        new("Architecht", 6, 12, 12),
        new("Hukuk ve Risk Takip", 3, 4, 4),
        new("KOBİ Bankacılığı", 2, 3, 3),
        new("Krediler", 2, 15, 15),
        new("Kurumsal ve Ticari Bankacılık", 2, 10, 25),
        new("Mali İşler", 2, 11, 11),
        new("Bireysel ve Özel Bankacılık", 1, 1, 3)
    };

    public List<SubeRow> Subeler { get; } = new()
    {
        new("Oprisk", "Adana Şubesi", RiskLevel.Dusuk),
        new("Oprisk", "Ağrı Şubesi", RiskLevel.Orta),
        new("Oprisk", "Akçaabat Şubesi", RiskLevel.Dusuk),
        new("İklim İç Ortam", "Akdeniz Sanayi Şubesi", RiskLevel.Dusuk),
        new("Oprisk", "Aksaray Metro Şubesi", RiskLevel.Yuksek),
        new("Oprisk", "Altunizade Şubesi", RiskLevel.Orta),
        new("Kredi", "Atışalanı Şubesi", RiskLevel.Dusuk),
        new("Oprisk", "Aydın Şubesi", RiskLevel.Orta),
        new("Oprisk", "Bağcılar Şubesi", RiskLevel.Dusuk),
        new("Kredi", "Bahçelievler Şubesi", RiskLevel.Yuksek),
        new("İklim İç Ortam", "Bakırköy Şubesi", RiskLevel.Dusuk),
        new("Oprisk", "Balıkesir Şubesi", RiskLevel.Orta),
        new("Oprisk", "Bandırma Şubesi", RiskLevel.Dusuk),
        new("Kredi", "Batman Şubesi", RiskLevel.Orta),
        new("Oprisk", "Bayrampaşa Şubesi", RiskLevel.Dusuk),
        new("Oprisk", "Beylikdüzü Şubesi", RiskLevel.Yuksek),
        new("İklim İç Ortam", "Bornova Şubesi", RiskLevel.Dusuk),
        new("Oprisk", "Bursa Şubesi", RiskLevel.Orta)
    };

    public List<StackedRow> PlanYiliBulgu { get; } = new()
    {
        new("2025", 14, 5, 1),
        new("2026", 59, 61, 13)
    };

    public List<CategoryValue> Portfoy { get; } = new()
    {
        new("BSD", 10, ReportColors.Blue),
        new("GMD", 6, ReportColors.Orange),
        new("KBD", 3, ReportColors.Magenta),
        new("GMK", 2, ReportColors.Purple),
        new("KT", 1, ReportColors.LightBlue)
    };

    public List<CategoryValue> DenetimTuru { get; } = new()
    {
        new("Oprisk", 66, ReportColors.Blue),
        new("Kredi", 14, ReportColors.Orange),
        new("İklim İç Ortam", 9, ReportColors.Magenta)
    };

    public List<CategoryValue> RaporGenelGorusu { get; } = new()
    {
        new("Gelişmesi Gerekli", 10, ReportColors.Yellow),
        new("Makul", 8, ReportColors.Green),
        new("(Boş)", 3, ReportColors.LightBlue)
    };

    public List<CategoryValue> RaporGorusSeviyeleri { get; } = new()
    {
        new("İyi", 51, ReportColors.LightBlue),
        new("Makul", 36, ReportColors.Green),
        new("Gelişmesi Gerekli", 2, ReportColors.Yellow),
        new("U/D", 1, ReportColors.Teal)
    };

    public List<CategoryValue> SubeBulguSeviyeleri { get; } = new()
    {
        new("Yüksek", 11, ReportColors.Red),
        new("Orta", 52, ReportColors.Orange),
        new("Düşük", 138, ReportColors.Teal)
    };

    public List<CategoryValue> BulguSurec { get; } = new()
    {
        new("Kredi Süreçleri", 41, ReportColors.Blue),
        new("Operasyon", 36, ReportColors.Blue),
        new("Bilgi Sistemleri", 29, ReportColors.Blue),
        new("Uyum ve Mevzuat", 24, ReportColors.Blue),
        new("İnsan Kaynakları", 13, ReportColors.Blue),
        new("Diğer", 10, ReportColors.Blue)
    };

    public List<ProjectionPoint> AksiyonProjeksiyon { get; } = new()
    {
        new("Tem", 24, 22), new("Ağu", 62, 55), new("Eyl", 98, 84),
        new("Eki", 140, 112), new("Kas", 176, null), new("Ara", 205, null)
    };

    public List<ProjectionPoint> BulguProjeksiyon { get; } = new()
    {
        new("Tem", 18, 16), new("Ağu", 46, 41), new("Eyl", 74, 66),
        new("Eki", 104, 92), new("Kas", 132, null), new("Ara", 153, null)
    };
}
