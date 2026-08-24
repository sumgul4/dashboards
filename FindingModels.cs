namespace TeftisAsistani.Models;

public sealed record FindingRequest(
    string Criterion,
    string Draft,
    bool KeepCriterionVerbatim,
    bool KeepDraftVerbatim,
    string Language);

public sealed record FindingRow(string Label, string Value);

public sealed record FindingTable(string Title, string FindingNo, IReadOnlyList<FindingRow> Rows);

public static class FindingTemplate
{
    /// AI'ın üretmesi gereken alanlar (kriter ve bulgu müfettişten gelir).
    public static readonly IReadOnlyList<string> GeneratedFields = new[]
    {
        "Kök neden", "Risk taksonomisi", "Risk", "Öneri", "Bulgu seviyesi"
    };

    /// Rapor şablonundaki satır sırası ve etiketleri (dile göre).
    public static IReadOnlyList<string> Labels(string language) => language == "en"
        ? new[] { "Criterion", "Finding", "Root cause", "Risk taxonomy", "Risk", "Recommendation", "Finding level" }
        : new[] { "Kriter", "Bulgu", "Kök neden", "Risk taksonomisi", "Risk", "Öneri", "Bulgu seviyesi" };

    public static string Title(string language) => language == "en" ? "FINDING TABLE" : "BULGU TABLOSU";
}
