using NextStep.Modules.Cv.Models;
using NextStep.Modules.Cv.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NextStep.Modules.Cv.Templates;

/// <summary>
/// LaTeX-inspired tech CV: single-column, dense but readable, ATS-friendly.
/// Designed for software engineers and technical roles.
/// </summary>
public class TechLatexCvDocument : IDocument, ICvTemplateStyle, ICustomSkillsLayoutStyle, ICustomLanguagesLayoutStyle
{
    private readonly CvData _data;

    public TechLatexCvDocument(CvData data)
    {
        _data = CvService.SanitizeCvData(data);
    }

    public string FontName => "Georgia";
    public string PrimaryColor => _data.ThemeColor ?? "#111827";

    private const string BodyColor = "#1F2937";
    private const string MutedColor = "#4B5563";
    private const string RuleColor = "#D1D5DB";
    private const string LinkColor = "#1D4ED8";

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(34);
            page.DefaultTextStyle(x => x.FontFamily(FontName).FontSize(9.5f).FontColor(BodyColor));

            page.Content().Column(column =>
            {
                ComposeHeader(column);

                foreach (var section in CvSectionMapper.VisibleSections(_data))
                    column.Item().Component(new CvSectionComponent(section, this));
            });
        });
    }

    public void DrawSectionTitle(ColumnDescriptor column, CvSection section)
    {
        column.Item().PaddingTop(10).PaddingBottom(4).Column(header =>
        {
            header.Item().Text(section.Title.ToUpperInvariant())
                .FontFamily(FontName).FontSize(10f).Bold().FontColor(PrimaryColor).LetterSpacing(0.08f);
            header.Item().PaddingTop(2).LineHorizontal(1.5f).LineColor(RuleColor);
        });
    }

    public void DrawSectionText(ColumnDescriptor column, CvSection section)
    {
        column.Item().PaddingBottom(6).Text(section.Text ?? string.Empty)
            .FontSize(9.2f).FontColor(BodyColor).LineHeight(1.45f);
    }

    public void DrawSectionItem(ColumnDescriptor column, CvSection section, CvSectionItem item)
    {
        switch (section.Type)
        {
            case CvSectionTypes.Experience:
                DrawExperience(column, item);
                break;
            case CvSectionTypes.Projects:
                DrawProject(column, item);
                break;
            case CvSectionTypes.Education:
                DrawEducation(column, item);
                break;
            case CvSectionTypes.Skills:
            case CvSectionTypes.SoftSkills:
                DrawInlineValue(column, item.PrimaryText, item.IsMatched);
                break;
            case CvSectionTypes.Activities:
                DrawActivity(column, item);
                break;
            case CvSectionTypes.Accomplishments:
            case CvSectionTypes.Certifications:
            case CvSectionTypes.Languages:
                DrawBulletItem(column, item.PrimaryText);
                break;
            default:
                DrawBulletItem(column, item.PrimaryText);
                break;
        }
    }

    public void DrawSectionDivider(ColumnDescriptor column, CvSection section)
    {
    }

    public void DrawSkillsSection(ColumnDescriptor column, CvSection section)
    {
        var skills = section.Items
            .Select(x => x.PrimaryText?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (skills.Count == 0)
            return;

        const int columnsCount = 4;
        column.Item().ShowEntire().Column(grid =>
        {
            for (var i = 0; i < skills.Count; i += columnsCount)
            {
                var rowItems = skills.Skip(i).Take(columnsCount).ToList();
                grid.Item().PaddingBottom(3).Row(row =>
                {
                    for (var colIndex = 0; colIndex < columnsCount; colIndex++)
                    {
                        var text = colIndex < rowItems.Count ? rowItems[colIndex] : string.Empty;
                        row.RelativeItem().Text(text)
                            .FontSize(9f)
                            .FontColor(BodyColor);
                    }
                });
            }
        });
    }

    public void DrawLanguagesSection(ColumnDescriptor column, CvSection section)
    {
        var languages = section.Items
            .Select(x => x.PrimaryText?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (languages.Count == 0)
            return;

        var textLine = string.Join("   •   ", languages);
        column.Item().PaddingBottom(4).Text(textLine)
            .FontSize(9.2f)
            .FontColor(BodyColor);
    }

    private void ComposeHeader(ColumnDescriptor column)
    {
        column.Item().AlignCenter().Text(_data.Candidate.Name)
            .FontFamily(FontName).FontSize(20).Bold().FontColor(PrimaryColor);

        column.Item().PaddingTop(6).AlignCenter().Text(ComposeContactLine())
            .FontSize(8.8f).FontColor(MutedColor);

        column.Item().PaddingTop(8).LineHorizontal(1).LineColor(RuleColor);
    }

    private string ComposeContactLine()
    {
        var values = new List<string>();
        Add(values, _data.Candidate.Email);
        Add(values, _data.Candidate.Phone);
        Add(values, _data.Candidate.Location);
        Add(values, _data.Candidate.LinkedIn);
        Add(values, _data.Candidate.GitHub);
        Add(values, _data.Candidate.Portfolio);
        return string.Join(" | ", values);
    }

    private static void Add(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values.Add(value);
    }

    private void DrawExperience(ColumnDescriptor column, CvSectionItem item)
    {
        column.Item().PaddingBottom(8).Column(block =>
        {
            block.Item().Row(row =>
            {
                row.RelativeItem().Text(item.PrimaryText).FontSize(10f).Bold().FontColor(PrimaryColor);
                var dateRange = ComposeDateRange(item);
                if (!string.IsNullOrWhiteSpace(dateRange))
                    row.AutoItem().Text(dateRange).FontSize(8.5f).FontColor(MutedColor);
            });

            if (!string.IsNullOrWhiteSpace(item.SecondaryText))
                block.Item().PaddingTop(1).Text(item.SecondaryText).FontSize(9f).Italic().FontColor(MutedColor);

            foreach (var bullet in item.Bullets)
                DrawBulletItem(block, bullet);
        });
    }

    private void DrawProject(ColumnDescriptor column, CvSectionItem item)
    {
        column.Item().PaddingBottom(8).Column(block =>
        {
            block.Item().Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span(item.PrimaryText).Bold().FontSize(10f).FontColor(PrimaryColor);
                    if (!string.IsNullOrWhiteSpace(item.Description))
                    {
                        text.Span(": ").Bold().FontSize(10f).FontColor(PrimaryColor);
                        text.Span(item.Description).FontSize(9.2f).FontColor(BodyColor);
                    }
                });

                var projectDate = FormatDateToken(item.StartDate);
                if (!string.IsNullOrWhiteSpace(projectDate))
                    row.AutoItem().Text(projectDate).FontSize(8.5f).FontColor(MutedColor);
            });

            if (!string.IsNullOrWhiteSpace(item.SecondaryText))
            {
                block.Item().PaddingTop(1).Text(text =>
                {
                    text.Span("Technologies: ").Bold().FontSize(8.8f).FontColor(PrimaryColor);
                    text.Span(item.SecondaryText).FontSize(8.8f).FontColor(BodyColor);
                });
            }

            foreach (var bullet in item.Bullets)
                DrawBulletItem(block, bullet);
        });
    }

    private void DrawEducation(ColumnDescriptor column, CvSectionItem item)
    {
        column.Item().PaddingBottom(7).Column(block =>
        {
            block.Item().Row(row =>
            {
                row.RelativeItem().Text(item.PrimaryText).FontSize(9.8f).Bold().FontColor(PrimaryColor);
                var period = ComposeEducationPeriod(item);
                if (!string.IsNullOrWhiteSpace(period))
                    row.AutoItem().Text(period).FontSize(8.5f).FontColor(MutedColor);
            });

            if (!string.IsNullOrWhiteSpace(item.SecondaryText))
                block.Item().PaddingTop(1).Text(item.SecondaryText).FontSize(9f).Italic().FontColor(MutedColor);
        });
    }

    private void DrawActivity(ColumnDescriptor column, CvSectionItem item)
    {
        if (string.IsNullOrWhiteSpace(item.PrimaryText))
            return;

        column.Item().PaddingBottom(4).Text(item.PrimaryText).FontSize(9.6f).Bold().FontColor(PrimaryColor);
    }

    private void DrawInlineValue(ColumnDescriptor column, string value, bool highlight)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        column.Item().PaddingBottom(3).Row(row =>
        {
            row.AutoItem().PaddingRight(6).Text("•").FontSize(9f).FontColor(highlight ? LinkColor : BodyColor);
            row.RelativeItem().Text(value).FontSize(9f).FontColor(BodyColor);
        });
    }

    private void DrawBulletItem(ColumnDescriptor column, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        column.Item().PaddingBottom(3).Row(row =>
        {
            row.AutoItem().PaddingRight(6).Text("•").FontSize(9f).FontColor(BodyColor);
            row.RelativeItem().Text(value).FontSize(9f).FontColor(BodyColor).LineHeight(1.35f);
        });
    }

    private static string ComposeDateRange(CvSectionItem item)
    {
        var start = FormatDateToken(item.StartDate);
        var end = FormatDateToken(item.EndDate);

        if (string.IsNullOrWhiteSpace(start) && string.IsNullOrWhiteSpace(end))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(end))
            return $"{start} - Present";

        if (string.IsNullOrWhiteSpace(start))
            return end;

        return $"{start} - {end}";
    }

    private static string ComposeEducationPeriod(CvSectionItem item)
    {
        if (string.IsNullOrWhiteSpace(item.StartDate) && string.IsNullOrWhiteSpace(item.EndDate))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(item.EndDate))
            return item.StartDate ?? string.Empty;

        if (string.IsNullOrWhiteSpace(item.StartDate))
            return item.EndDate ?? string.Empty;

        return $"{item.StartDate} - {item.EndDate}";
    }

    private static string ComposeRoleDescription(string? role, string? description)
    {
        var cleanRole = role?.Trim();
        var cleanDescription = description?.Trim();

        if (string.IsNullOrWhiteSpace(cleanRole))
            return cleanDescription ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cleanDescription))
            return cleanRole;

        return $"{cleanRole}: {cleanDescription}";
    }

    private static string FormatDateToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        if (DateTime.TryParse(raw, out var parsed))
            return parsed.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

        return raw.Trim();
    }
}
