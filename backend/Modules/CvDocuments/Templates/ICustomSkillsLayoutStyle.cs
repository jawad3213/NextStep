using NextStep.Modules.CvDocuments.Domain;
using QuestPDF.Fluent;

namespace NextStep.Modules.CvDocuments.Templates;

public interface ICustomSkillsLayoutStyle
{
    void DrawSkillsSection(ColumnDescriptor column, CvSection section);
}
