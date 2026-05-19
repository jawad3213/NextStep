using NextStep.Modules.Cv.Models;
using QuestPDF.Fluent;

namespace NextStep.Modules.Cv.Templates;

public interface ICustomSkillsLayoutStyle
{
    void DrawSkillsSection(ColumnDescriptor column, CvSection section);
}
