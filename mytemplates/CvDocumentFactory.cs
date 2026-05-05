using QuestPDF.Infrastructure;
using CvManagementApp.Models;

namespace CvManagementApp.Templates;

/// <summary>
/// Factory to create the correct IDocument based on template ID.
/// </summary>
public static class CvDocumentFactory
{
    public static IDocument Create(string templateId, CvData data)
        => templateId switch
        {
            "modern"    => new ModernCvDocument(data),
            "classic"   => new ClassicCvDocument(data),
            "executive" => new ExecutiveCvDocument(data),
            "protemplate" => new ProCvDocument(data),
            "elegant"     => new ElegantCvDocument(data),
            _ => throw new ArgumentException($"Unknown template: {templateId}")
        };
}
