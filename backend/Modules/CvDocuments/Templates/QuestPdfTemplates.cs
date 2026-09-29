using NextStep.Modules.CvDocuments.Domain;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace NextStep.Modules.CvDocuments.Templates;

/// <summary>
/// The QuestPDF side of the template catalogue. The HTML renderer owns the authoritative
/// slug list; this must cover it, because CvService uses it as the fallback whenever
/// Puppeteer is unavailable, times out, or fails.
/// </summary>
public static class QuestPdfTemplates
{
    public static IDocument Create(string templateId, CvData data) => Resolve(templateId)(data);

    public static bool Covers(string templateId) => TryNormalize(templateId, out _);

    private static Func<CvData, IDocument> Resolve(string templateId)
        => TryNormalize(templateId, out var factory)
            ? factory
            : throw new ArgumentException(
                $"Unknown CV template: '{templateId}'. " +
                $"Valid: {string.Join(", ", Supported)}. Aliases: {string.Join(", ", AliasNames)}.");

    private static bool TryNormalize(string templateId, out Func<CvData, IDocument> factory)
    {
        var normalized = (templateId ?? string.Empty).Trim().ToLowerInvariant();
        var slug = Aliases.TryGetValue(normalized, out var alias) ? alias : normalized;

        factory = Slugs.TryGetValue(slug, out var found) ? found : null!;
        return factory is not null;
    }

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["tech-latex"] = "latex",
        ["tech_latex"] = "latex"
    };

    private static readonly Dictionary<string, Func<CvData, IDocument>> Slugs = new(StringComparer.Ordinal)
    {
        ["modern"] = data => new ModernCvDocument(data),
        ["latex"] = data => new TechLatexCvDocument(data),
        ["executive"] = data => new ExecutiveCvDocument(data),
        ["horizon"] = data => new ElegantCvDocument(data)
    };

    private static readonly string[] Supported = ["modern", "latex", "executive", "horizon"];

    private static readonly string[] AliasNames = ["tech-latex", "tech_latex"];
}
