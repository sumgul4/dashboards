using System.Text;
using System.Text.Json;
using TeftisAsistani.Models;

namespace TeftisAsistani.Services;

/// LLM entegrasyonunun tek yeri. Şirket içi model / Azure OpenAI / vb. burada çağrılır.
public sealed class AuditAiService : IAuditAiService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AuditAiService(HttpClient http) => _http = http;

    public async Task<PromptResult> RunPromptAsync(PromptRequest request, CancellationToken ct = default)
    {
        var system = "Sen bir katılım bankasının iç denetim biriminde çalışan kıdemli bir müfettiş asistanısın. " +
                     "Tarafsız, tespit odaklı, kurumsal denetim dili kullan. Uydurma bilgi ekleme.";

        var user = new StringBuilder()
            .AppendLine(request.Prompt)
            .AppendLine()
            .AppendLine("### METİN")
            .AppendLine(request.Text)
            .AppendLine()
            .AppendLine("Yanıtı şu JSON şemasıyla ver: { \"heading\": string, \"body\": string, \"notes\": string[] }")
            .ToString();

        var payload = await CompleteAsync(system, user, request.Language, ct);
        return JsonSerializer.Deserialize<PromptResult>(payload, Json)
               ?? new PromptResult("Sonuç", payload, Array.Empty<string>());
    }

    public async Task<FindingTable> BuildFindingTableAsync(FindingRequest request, CancellationToken ct = default)
    {
        var labels = FindingTemplate.Labels(request.Language);

        var system = "Sen bir katılım bankasının iç denetim raporlarını hazırlayan müfettiş asistanısın. " +
                     "Rapor şablonundaki alanları, verilen kriter ve bulgu taslağına dayanarak doldur. " +
                     "Kriter dışına çıkma, mevzuat maddesi uydurma.";

        var rules = new List<string>
        {
            request.KeepCriterionVerbatim
                ? "Kriter metnini AYNEN aktar; paraphrase etme, tek kelimesini değiştirme."
                : "Kriter metnini kurumsal rapor diline uygun şekilde yeniden yaz (paraphrase).",
            request.KeepDraftVerbatim
                ? "Bulgu taslağını AYNEN aktar; paraphrase etme."
                : "Bulgu taslağını kurumsal rapor diline uygun şekilde yeniden yaz (paraphrase)."
        };

        var user = new StringBuilder()
            .AppendLine("### KRİTER").AppendLine(request.Criterion).AppendLine()
            .AppendLine("### BULGU TASLAĞI").AppendLine(request.Draft).AppendLine()
            .AppendLine("### KURALLAR")
            .AppendLine(string.Join(Environment.NewLine, rules.Select(r => "- " + r)))
            .AppendLine("- 'Bulgu seviyesi' değeri yalnızca Yüksek / Orta / Düşük olabilir.")
            .AppendLine("- 'Risk taksonomisi' değeri 'Ana kategori › Alt kategori › Detay' biçiminde olsun.")
            .AppendLine()
            .AppendLine("### İSTENEN ALANLAR (bu sırayla)")
            .AppendLine(string.Join(", ", labels))
            .AppendLine()
            .AppendLine("Yanıtı şu JSON şemasıyla ver: { \"rows\": [ { \"label\": string, \"value\": string } ] }")
            .ToString();

        var payload = await CompleteAsync(system, user, request.Language, ct);
        var parsed = JsonSerializer.Deserialize<RowsEnvelope>(payload, Json);

        var rows = parsed?.Rows ?? new List<FindingRow>();

        // Paraphrase kapalıysa müfettişin girdisini garanti altına al.
        rows = rows.Select(r =>
            r.Label == labels[0] && request.KeepCriterionVerbatim ? r with { Value = request.Criterion } :
            r.Label == labels[1] && request.KeepDraftVerbatim ? r with { Value = request.Draft } : r).ToList();

        return new FindingTable(
            Title: FindingTemplate.Title(request.Language),
            FindingNo: $"BLG-{DateTime.Now:yyyy}-{Random.Shared.Next(1, 999):000}",
            Rows: rows);
    }

    private async Task<string> CompleteAsync(string system, string user, string language, CancellationToken ct)
    {
        var body = new
        {
            model = "gpt-4o-mini",
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = system + AppLanguage.Instruction(language) },
                new { role = "user", content = user }
            }
        };

        using var res = await _http.PostAsJsonAsync("chat/completions", body, ct);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("choices")[0]
                  .GetProperty("message").GetProperty("content").GetString() ?? "{}";
    }

    private sealed class RowsEnvelope
    {
        public List<FindingRow> Rows { get; set; } = new();
    }
}
