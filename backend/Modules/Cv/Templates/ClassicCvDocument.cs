using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NextStep.Modules.Cv.Models;
using System.Collections.Generic;
using System.Linq;

namespace NextStep.Modules.Cv.Templates;

/// <summary>
/// Classic CV template: A4, margin 40, centered header with no background.
/// Single column layout. Georgia font for Name, Inter for everything else.
/// Sections separated by horizontal divider lines.
/// </summary>
public class ClassicCvDocument : IDocument
{
    private readonly CvData _data;
    public ClassicCvDocument(CvData data) { _data = data; }

    private string BodyFont => _data.FontFamily ?? "Inter";
    private string NameFont => _data.FontFamily ?? "Georgia";
    private string TitleColor => _data.ThemeColor ?? "#1a1a2e";

    private const string BodyColor = "#333333";
    private const string GrayText = "#666666";
    private const string DividerColor = "#cccccc";

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);

            page.Content().Column(column =>
            {
                ComposeHeader(column);
                ComposeDivider(column);

                if (!string.IsNullOrWhiteSpace(_data.Summary))
                {
                    ComposeSectionTitle(column, "Summary");
                    column.Item().PaddingBottom(10).Text(_data.Summary)
                        .FontFamily(BodyFont).FontSize(10).FontColor(BodyColor).LineHeight(1.6f);
                }

                ComposeDivider(column);

                if (_data.Experience is { Count: > 0 })
                {
                    ComposeSectionTitle(column, "Experience");
                    foreach (var exp in _data.Experience)
                        ComposeExperience(column, exp);
                }

                if (_data.Projects is { Count: > 0 })
                {
                    ComposeDivider(column);
                    ComposeSectionTitle(column, "Projects");
                    foreach (var prj in _data.Projects)
                        ComposeProject(column, prj);
                }

                if (_data.Certifications is { Count: > 0 })
                {
                    ComposeDivider(column);
                    ComposeSectionTitle(column, "Certifications");
                    foreach (var cert in _data.Certifications)
                    {
                        column.Item().PaddingBottom(4).Row(row =>
                        {
                            row.AutoItem().PaddingRight(5).Text("•")
                                .FontFamily(BodyFont).FontSize(10).FontColor(BodyColor);
                            row.RelativeItem().Text(cert)
                                .FontFamily(BodyFont).FontSize(10).FontColor(BodyColor);
                        });
                    }
                }

                if (_data.Activities is { Count: > 0 })
                {
                    ComposeDivider(column);
                    ComposeSectionTitle(column, "Activities");
                    foreach (var act in _data.Activities)
                        ComposeActivity(column, act);
                }

                ComposeDivider(column);

                if (_data.Skills is { Count: > 0 })
                {
                    ComposeSectionTitle(column, "Skills");
                    column.Item().PaddingBottom(10)
                        .Text(string.Join(", ", _data.Skills.Select(s => s.Name)))
                        .FontFamily(BodyFont).FontSize(10).FontColor(BodyColor);
                }

                ComposeDivider(column);

                if (_data.Education is { Count: > 0 })
                {
                    ComposeSectionTitle(column, "Education");
                    foreach (var edu in _data.Education)
                        ComposeEducation(column, edu);
                }

                if (_data.Languages is { Count: > 0 })
                {
                    ComposeDivider(column);
                    ComposeSectionTitle(column, "Languages");
                    column.Item().PaddingBottom(10).Text(string.Join(", ", _data.Languages))
                        .FontFamily(BodyFont).FontSize(10).FontColor(BodyColor);
                }
            });
        });
    }

    private void ComposeHeader(ColumnDescriptor column)
    {
        column.Item().AlignCenter().Text(_data.Candidate.Name)
            .FontFamily(NameFont).FontSize(24).Bold().FontColor(TitleColor);

        column.Item().PaddingTop(4).AlignCenter().Row(row =>
        {
            var parts = new List<string> { _data.Candidate.Email };
            if (_data.Candidate.Phone is not null) parts.Add(_data.Candidate.Phone);
            if (_data.Candidate.Location is not null) parts.Add(_data.Candidate.Location);
            row.AutoItem().Text(string.Join("  ·  ", parts))
                .FontFamily(BodyFont).FontSize(10).FontColor(GrayText);
        });

        column.Item().PaddingTop(10);
    }

    private static void ComposeSectionTitle(ColumnDescriptor column, string title)
    {
        column.Item().PaddingBottom(8).Text(title.ToUpper())
            .FontFamily(BodyFont).FontSize(13).Bold().FontColor(TitleColor);
    }

    private static void ComposeDivider(ColumnDescriptor column)
    {
        column.Item().PaddingVertical(8).LineHorizontal(1).LineColor(DividerColor);
    }

    private static void ComposeExperience(ColumnDescriptor column, CvExperience exp)
    {
        var dateStr = exp.End is not null ? $"{exp.Start} – {exp.End}" : $"{exp.Start} – Present";
        column.Item().PaddingBottom(12).Column(item =>
        {
            item.Item().Row(r =>
            {
                r.RelativeItem().Text(exp.Role).FontFamily(BodyFont).FontSize(11).Bold().FontColor(TitleColor);
                r.AutoItem().Text(dateStr).FontFamily(BodyFont).FontSize(9).FontColor(GrayText);
            });
            item.Item().PaddingTop(1).Text(exp.Company).FontFamily(BodyFont).FontSize(10).Italic().FontColor(GrayText);
            if (exp.Bullets is { Count: > 0 })
                item.Item().PaddingTop(4).Column(bullets =>
                {
                    foreach (var b in exp.Bullets)
                        bullets.Item().PaddingBottom(3).Row(row =>
                        {
                            row.AutoItem().PaddingRight(5).Text("•").FontFamily(BodyFont).FontSize(10).FontColor(BodyColor);
                            row.RelativeItem().Text(b).FontFamily(BodyFont).FontSize(10).FontColor(BodyColor).LineHeight(1.5f);
                        });
                });
        });
    }

    private static void ComposeProject(ColumnDescriptor column, CvProject prj)
    {
        column.Item().PaddingBottom(10).Column(item =>
        {
            item.Item().Text(prj.Title).FontFamily(BodyFont).FontSize(11).Bold().FontColor(TitleColor);
            if (!string.IsNullOrWhiteSpace(prj.Description))
                item.Item().PaddingTop(1).Text(prj.Description).FontFamily(BodyFont).FontSize(10).Italic().FontColor(GrayText);
            if (prj.Bullets is { Count: > 0 })
                item.Item().PaddingTop(4).Column(bullets =>
                {
                    foreach (var b in prj.Bullets)
                        bullets.Item().PaddingBottom(3).Row(row =>
                        {
                            row.AutoItem().PaddingRight(5).Text("•").FontFamily(BodyFont).FontSize(10).FontColor(BodyColor);
                            row.RelativeItem().Text(b).FontFamily(BodyFont).FontSize(10).FontColor(BodyColor).LineHeight(1.5f);
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
                r.AutoItem().Text(act.Title).FontFamily(BodyFont).FontSize(11).Bold().FontColor(TitleColor);
                if (!string.IsNullOrWhiteSpace(act.Role))
                    r.AutoItem().Text($"  —  {act.Role}").FontFamily(BodyFont).FontSize(10).FontColor(GrayText);
            });
            if (!string.IsNullOrWhiteSpace(act.Description))
                item.Item().PaddingTop(2).Text(act.Description).FontFamily(BodyFont).FontSize(10).FontColor(BodyColor).LineHeight(1.5f);
        });
    }

    private static void ComposeEducation(ColumnDescriptor column, CvEducation edu)
    {
        column.Item().PaddingBottom(10).Column(item =>
        {
            item.Item().Text(edu.Degree).FontFamily(BodyFont).FontSize(11).Bold().FontColor(TitleColor);
            item.Item().PaddingTop(1).Text(edu.Institution).FontFamily(BodyFont).FontSize(10).Italic().FontColor(GrayText);
            item.Item().PaddingTop(1).Text(edu.Year).FontFamily(BodyFont).FontSize(9).FontColor(GrayText);
        });
    }
}
