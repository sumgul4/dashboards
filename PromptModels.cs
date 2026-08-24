namespace TeftisAsistani.Models;

public sealed record PromptDefinition(string Id, string Title, string Hint, string Template);

public sealed record PromptRequest(string PromptId, string Prompt, string Text, string Language);

public sealed record PromptResult(string Heading, string Body, IReadOnlyList<string> Notes);

public static class AppLanguage
{
    public sealed record Item(string Code, string Display);

    public static readonly IReadOnlyList<Item> All = new[]
    {
        new Item("tr", "Türkçe"),
        new Item("en", "English")
    };

    public static string Display(string code) =>
        All.FirstOrDefault(x => x.Code == code)?.Display ?? "Türkçe";

    public static string Instruction(string code) =>
        code == "en" ? " Respond in English." : " Yanıtı Türkçe ver.";
}

public static class PromptLibrary
{
    public static readonly IReadOnlyList<PromptDefinition> Items = new[]
    {
        new PromptDefinition("grammar", "Gramer kontrolü yap",
            "Yazım, noktalama ve anlatım hatalarını düzeltir.",
            "Aşağıdaki metnin yazım, noktalama ve anlatım hatalarını düzelt. Anlamı ve teknik terimleri koru. Düzeltilen noktaları kısaca listele."),
        new PromptDefinition("risk", "Risk kategorisini bul",
            "Metni kurumsal risk taksonomisine eşler.",
            "Aşağıdaki metni kurumun risk taksonomisine göre değerlendir; ana risk kategorisini, alt kategoriyi ve gerekçesini belirt."),
        new PromptDefinition("summary", "Metni özetle",
            "Rapora uygun kısa özet üretir.",
            "Aşağıdaki metni denetim raporuna uygun, en fazla 4 cümlelik kurumsal bir özete dönüştür."),
        new PromptDefinition("coso", "COSO ile eşleştir",
            "İlgili COSO bileşeni ve ilkesini bulur.",
            "Aşağıdaki metni COSO iç kontrol çerçevesiyle eşleştir. İlgili bileşeni, ilkeyi (1-17) ve eşleşme gerekçesini belirt."),
        new PromptDefinition("tone", "Üslup / kurumsal dil düzeltme",
            "Tarafsız, resmi denetim diline çevirir.",
            "Aşağıdaki metni tarafsız, resmi ve kurumsal denetim diline çevir. Suçlayıcı ifadelerden kaçın, tespit odaklı yaz.")
    };

    public static string Compose(PromptDefinition p, string language) =>
        p.Template + AppLanguage.Instruction(language);
}
