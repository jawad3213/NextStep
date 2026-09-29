using NextStep.Modules.CvDocuments.Domain;
using QuestPDF.Fluent;

namespace NextStep.Modules.CvDocuments.Templates;

public interface ICustomLanguagesLayoutStyle
{
    void DrawLanguagesSection(ColumnDescriptor column, CvSection section);
}
