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
            "chrono" or "classic"    => new ClassicCvDocument(data),
            "elegant"                => new ElegantCvDocument(data),
            "circular"               => new CircularCvDocument(data),
            "modern"                 => new ModernCvDocument(data),
            "luxe" or "executive"    => new ExecutiveCvDocument(data),
            "pro"                    => new ProCvDocument(data),
            _ => throw new ArgumentException($"Unknown CV template: '{templateId}'. Valid: chrono, elegant, circular, modern, luxe.")
        };
}
