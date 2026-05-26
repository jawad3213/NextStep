using NextStep.Modules.Cv.Models;
using QuestPDF.Fluent;

namespace NextStep.Modules.Cv.Templates;

public interface ICvTemplateStyle
{
    string FontName { get; }
    string PrimaryColor { get; }

    void DrawSectionTitle(ColumnDescriptor column, CvSection section);
    void DrawSectionText(ColumnDescriptor column, CvSection section);
    void DrawSectionItem(ColumnDescriptor column, CvSection section, CvSectionItem item);
    void DrawSectionDivider(ColumnDescriptor column, CvSection section);
}
