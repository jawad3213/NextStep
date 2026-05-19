using NextStep.Modules.Cv.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace NextStep.Modules.Cv.Templates;

public class CvSectionComponent : IComponent
{
    private readonly CvSection _section;
    private readonly ICvTemplateStyle _style;

    public CvSectionComponent(CvSection section, ICvTemplateStyle style)
    {
        _section = section;
        _style = style;
    }

    public void Compose(IContainer container)
    {
        if (!_section.IsVisible)
            return;

        // Keep small/standard sections together (Skills, Education, Languages, Certifications, Summary)
        // For large lists like Experience or Projects, we keep individual items together, but allow the section to span pages.
        var shouldShowEntire = _section.Type == CvSectionTypes.Skills ||
                               _section.Type == CvSectionTypes.SoftSkills ||
                               _section.Type == CvSectionTypes.Education ||
                               _section.Type == CvSectionTypes.Languages ||
                               _section.Type == CvSectionTypes.Certifications ||
                               _section.Type == CvSectionTypes.Summary;

        var targetContainer = shouldShowEntire ? container.ShowEntire() : container;

        targetContainer.Column(column =>
        {
            _style.DrawSectionTitle(column, _section);

            if (!string.IsNullOrWhiteSpace(_section.Text))
                _style.DrawSectionText(column, _section);

            if ((_section.Type == CvSectionTypes.Skills || _section.Type == CvSectionTypes.SoftSkills) && _style is ICustomSkillsLayoutStyle customSkillsLayoutStyle)
            {
                customSkillsLayoutStyle.DrawSkillsSection(column, _section);
                _style.DrawSectionDivider(column, _section);
                return;
            }

            if (_section.Type == CvSectionTypes.Languages && _style is ICustomLanguagesLayoutStyle customLanguagesLayoutStyle)
            {
                customLanguagesLayoutStyle.DrawLanguagesSection(column, _section);
                _style.DrawSectionDivider(column, _section);
                return;
            }

            foreach (var item in _section.Items)
            {
                // For experience and projects, keep each individual item together!
                if (_section.Type == CvSectionTypes.Experience || _section.Type == CvSectionTypes.Projects)
                {
                    column.Item().ShowEntire().Column(itemCol => 
                    {
                        _style.DrawSectionItem(itemCol, _section, item);
                    });
                }
                else
                {
                    _style.DrawSectionItem(column, _section, item);
                }
            }

            _style.DrawSectionDivider(column, _section);
        });
    }
}
