using QuestPDF.Infrastructure;
using NextStep.Modules.Cv.Models;

namespace NextStep.Modules.Cv.Templates;

/// <summary>
/// Creates the correct IDocument based on the chosen template ID.
/// </summary>
public static class CvDocumentFactory
{
    public static IDocument Create(string templateId, CvData data)
        => templateId.ToLowerInvariant() switch
        {
            "modern"    => new ModernCvDocument(data),
            "classic"   => new ClassicCvDocument(data),
            "executive" => new ExecutiveCvDocument(data),
            "pro"       => new ProCvDocument(data),
            "elegant"   => new ElegantCvDocument(data),
            _ => throw new ArgumentException($"Unknown CV template: '{templateId}'. Valid: modern, classic, executive, pro, elegant.")
        };
}
