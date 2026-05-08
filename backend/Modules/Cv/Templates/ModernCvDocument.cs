using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NextStep.Modules.Cv.Models;

namespace NextStep.Modules.Cv.Templates;

/// <summary>
/// Modern CV template: A4, full-width dark blue header, two-column body layout.
/// Left column (35%): Skills, Education, Languages.
/// Right column (65%): Summary, Experience, Projects, Activities, Certifications.
/// </summary>
public class ModernCvDocument : IDocument
{
    private readonly CvData _data;
    public ModernCvDocument(CvData data) { _data = data; }
    
    private string FontName => _data.FontFamily ?? "Inter";
    private string HeaderBg => _data.ThemeColor ?? "#2d3a8c";
    private string AccentColor => _data.ThemeColor ?? "#2d3a8c";
    
    private const string HeaderLightText = "#aab4e8";
    private const string SidebarBg = "#f5f5f5";
    private const string TitleColor = "#1a1a2e";
    private const string BodyColor = "#333333";
    private const string GrayText = "#666666";

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);

            page.Header().Background(HeaderBg).Padding(25).Column(column =>
            {
                column.Item().Text(_data.Candidate.Name)
                    .FontFamily(FontName).FontSize(26).Bold().FontColor("#ffffff");

                column.Item().PaddingTop(4).Row(row =>
                {
                    row.AutoItem().Text(_data.Candidate.Email)
                        .FontFamily(FontName).FontSize(10).FontColor(HeaderLightText);

                    if (_data.Candidate.Location is not null)
                    {
                        row.AutoItem().PaddingHorizontal(8).Text("·").FontFamily(FontName).FontSize(10).FontColor(HeaderLightText);
                        row.AutoItem().Text(_data.Candidate.Location).FontFamily(FontName).FontSize(10).FontColor(HeaderLightText);
                    }

                    if (_data.Candidate.Phone is not null)
                    {
                        row.AutoItem().PaddingHorizontal(8).Text("·").FontFamily(FontName).FontSize(10).FontColor(HeaderLightText);
                        row.AutoItem().Text(_data.Candidate.Phone).FontFamily(FontName).FontSize(10).FontColor(HeaderLightText);
                    }
                });
            });

            page.Content().Row(row =>
            {
                row.RelativeItem(35).Background(SidebarBg).Padding(20).Column(ComposeLeftColumn);
                row.RelativeItem(65).Background("#ffffff").Padding(20).Column(ComposeRightColumn);
            });
        });
    }

    private void ComposeLeftColumn(ColumnDescriptor column)
    {
        if (_data.Skills is { Count: > 0 })
        {
            ComposeSectionTitle(column, "Skills");
            foreach (var skill in _data.Skills)
                column.Item().PaddingBottom(4).Row(row =>
                {
                    row.AutoItem().PaddingRight(5).Text("•").FontFamily(FontName).FontSize(9).FontColor(skill.IsMatched ? AccentColor : BodyColor);
                    row.RelativeItem().Text(skill.Name).FontFamily(FontName).FontSize(9).FontColor(BodyColor);
                });
        }

        column.Item().PaddingTop(14);

        if (_data.Education is { Count: > 0 })
        {
            ComposeSectionTitle(column, "Education");
            foreach (var edu in _data.Education)
                column.Item().PaddingBottom(10).Column(e =>
                {
                    e.Item().Text(edu.Degree).FontFamily(FontName).FontSize(10).Bold().FontColor(TitleColor);
                    e.Item().PaddingTop(1).Text(edu.Institution).FontFamily(FontName).FontSize(9).Italic().FontColor(GrayText);
                    e.Item().PaddingTop(1).Text(edu.Year).FontFamily(FontName).FontSize(9).FontColor(GrayText);
                });
        }

        if (_data.Languages is { Count: > 0 })
        {
            column.Item().PaddingTop(14);
            ComposeSectionTitle(column, "Languages");
            foreach (var lang in _data.Languages)
                column.Item().PaddingBottom(3).Text(lang).FontFamily(FontName).FontSize(9).FontColor(BodyColor);
        }
    }

    private void ComposeRightColumn(ColumnDescriptor column)
    {
        if (!string.IsNullOrWhiteSpace(_data.Summary))
        {
            ComposeSectionTitle(column, "Summary");
            column.Item().PaddingBottom(10).Text(_data.Summary).FontFamily(FontName).FontSize(10).FontColor(BodyColor).LineHeight(1.6f);
        }

        if (_data.Experience is { Count: > 0 })
        {
            ComposeSectionTitle(column, "Experience");
            foreach (var exp in _data.Experience)
                ComposeExperience(column, exp);
        }

        if (_data.Projects is { Count: > 0 })
        {
            ComposeSectionTitle(column, "Projects");
            foreach (var prj in _data.Projects)
                ComposeProject(column, prj);
        }

        if (_data.Certifications is { Count: > 0 })
        {
            ComposeSectionTitle(column, "Certifications");
            foreach (var cert in _data.Certifications)
                column.Item().PaddingBottom(4).Row(row =>
                {
                    row.AutoItem().PaddingRight(5).Text("•").FontFamily(FontName).FontSize(9).FontColor(AccentColor);
                    row.RelativeItem().Text(cert).FontFamily(FontName).FontSize(9).FontColor(BodyColor);
                });
        }

        if (_data.Activities is { Count: > 0 })
        {
            ComposeSectionTitle(column, "Activities");
            foreach (var act in _data.Activities)
                ComposeActivity(column, act);
        }
    }

    private static void ComposeSectionTitle(ColumnDescriptor column, string title)
    {
        column.Item().PaddingBottom(6).Column(titleCol =>
        {
            titleCol.Item().Text(title).FontFamily(FontName).FontSize(12).Bold().FontColor("#1a1a2e");
            titleCol.Item().PaddingTop(2).LineHorizontal(1).LineColor(AccentColor);
        });
    }

    private static void ComposeExperience(ColumnDescriptor column, CvExperience exp)
    {
        var dateStr = exp.End is not null ? $"{exp.Start} – {exp.End}" : $"{exp.Start} – Present";
        column.Item().PaddingBottom(12).Column(item =>
        {
            item.Item().Row(r =>
            {
                r.RelativeItem().Text(exp.Role).FontFamily(FontName).FontSize(11).Bold().FontColor("#1a1a2e");
                r.AutoItem().Text(dateStr).FontFamily(FontName).FontSize(9).FontColor(GrayText);
            });
            item.Item().PaddingTop(1).Text(exp.Company).FontFamily(FontName).FontSize(10).Italic().FontColor(GrayText);
            if (exp.Bullets is { Count: > 0 })
                item.Item().PaddingTop(4).Column(bullets =>
                {
                    foreach (var b in exp.Bullets)
                        bullets.Item().PaddingBottom(3).Row(row =>
                        {
                            row.AutoItem().PaddingRight(5).Text("•").FontFamily(FontName).FontSize(9).FontColor(BodyColor);
                            row.RelativeItem().Text(b).FontFamily(FontName).FontSize(9).FontColor(BodyColor).LineHeight(1.5f);
                        });
                });
        });
    }

    private static void ComposeProject(ColumnDescriptor column, CvProject prj)
    {
        column.Item().PaddingBottom(10).Column(item =>
        {
            item.Item().Text(prj.Title).FontFamily(FontName).FontSize(11).Bold().FontColor("#1a1a2e");
            if (!string.IsNullOrWhiteSpace(prj.Description))
                item.Item().PaddingTop(1).Text(prj.Description).FontFamily(FontName).FontSize(9).Italic().FontColor(GrayText);
            if (prj.Bullets is { Count: > 0 })
                item.Item().PaddingTop(4).Column(bullets =>
                {
                    foreach (var b in prj.Bullets)
                        bullets.Item().PaddingBottom(3).Row(row =>
                        {
                            row.AutoItem().PaddingRight(5).Text("•").FontFamily(FontName).FontSize(9).FontColor(BodyColor);
                            row.RelativeItem().Text(b).FontFamily(FontName).FontSize(9).FontColor(BodyColor).LineHeight(1.5f);
                        });
                });
        });
    }

    private static void ComposeActivity(ColumnDescriptor column, CvActivity act)
    {
        column.Item().PaddingBottom(8).Column(item =>
        {
            item.Item().Row(r =>
            {
                r.AutoItem().Text(act.Title).FontFamily(FontName).FontSize(11).Bold().FontColor("#1a1a2e");
                if (!string.IsNullOrWhiteSpace(act.Role))
                    r.AutoItem().Text($"  —  {act.Role}").FontFamily(FontName).FontSize(10).FontColor(GrayText);
            });
            if (!string.IsNullOrWhiteSpace(act.Description))
                item.Item().PaddingTop(2).Text(act.Description).FontFamily(FontName).FontSize(9).FontColor(BodyColor).LineHeight(1.5f);
        });
    }
}
