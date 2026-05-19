using NextStep.Modules.Cv.Models;
using QuestPDF.Fluent;

namespace NextStep.Modules.Cv.Templates;

public interface ICustomLanguagesLayoutStyle
{
    void DrawLanguagesSection(ColumnDescriptor column, CvSection section);
}
