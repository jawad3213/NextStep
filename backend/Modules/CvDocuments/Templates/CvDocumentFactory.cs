using QuestPDF.Infrastructure;
using NextStep.Modules.CvDocuments.Domain;

namespace NextStep.Modules.CvDocuments.Templates;

/// <summary>
/// Creates the correct IDocument based on the chosen template ID.
/// </summary>
public static class CvDocumentFactory
{
    public static IDocument Create(string templateId, CvData data) => QuestPdfTemplates.Create(templateId, data);
}

