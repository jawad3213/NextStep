using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NextStep.Modules.Cv.Models;

namespace NextStep.Modules.Cv.Templates;

/// <summary>
/// Polished Pro CV — two-column layout with skill bars, summary band, and SVG contact chips.
/// </summary>
public class ProCvDocument : IDocument
{
    private readonly CvData _data;
    public ProCvDocument(CvData data) => _data = data;

    private string SerifFont => _data.FontFamily ?? "Georgia";
    private string SansFont => _data.FontFamily ?? "Inter";
    private string Navy => _data.ThemeColor ?? "#1C2440";

    private const string NavyMid     = "#4A5270";
    private const string Muted       = "#6B7492";
    private const string Faint       = "#9198B0";
    private const string RuleLight   = "#E2E5EF";
    private const string SummaryBg   = "#F7F8FC";
    private const string White       = "#FFFFFF";

    private const float PageMargin   = 32f;
    private const float SidebarWidth = 148f;
    private const float DateColWidth = 70f;
    private const float PhotoW       = 78f;
    private const float PhotoH       = 88f;

    private const string SvgLocation = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='#ffffff'><path d='M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z'/></svg>";
    private const string SvgPhone    = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='#ffffff'><path d='M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z'/></svg>";
    private const string SvgEmail    = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='#ffffff'><path d='M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z'/></svg>";

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(PageMargin);
            page.PageColor(White);
            page.DefaultTextStyle(x => x.FontFamily(SansFont).FontSize(9).FontColor(Navy));

            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(_data.Candidate.Name.ToUpperInvariant())
                           .FontFamily(SerifFont).FontSize(34).ExtraBold().LetterSpacing(-0.02f);
                        col.Item().PaddingTop(-2).Text("PROFESSIONAL RÉSUMÉ")
                           .FontSize(10).SemiBold().FontColor(Muted).LetterSpacing(0.2f);
                    });

                    row.AutoItem().Width(PhotoW).Height(PhotoH).Background(Navy).AlignCenter().AlignMiddle()
                       .Text(GetInitials(_data.Candidate.Name)).FontSize(22).Bold().FontColor(White);
                });

                header.Item().PaddingVertical(16).Row(row =>
                {
                    ContactChip(row, SvgLocation, _data.Candidate.Location);
                    ContactChip(row, SvgPhone,    _data.Candidate.Phone);
                    ContactChip(row, SvgEmail,    _data.Candidate.Email);
                });

                header.Item().Height(1).Background(RuleLight);
            });

            page.Content().PaddingTop(20).Column(content =>
            {
                if (!string.IsNullOrWhiteSpace(_data.Summary))
                {
                    content.Item().Background(SummaryBg).Padding(14).Column(col =>
                    {
                        col.Item().Text("PROFESSIONAL SUMMARY").FontSize(8).Bold().FontColor(NavyMid).LetterSpacing(0.1f);
                        col.Item().PaddingTop(4).Text(_data.Summary).LineHeight(1.6f);
                    });
                }

                content.Item().PaddingTop(24).Row(body =>
                {
                    body.RelativeItem().PaddingRight(24).Column(ComposeMainContent);
                    body.ConstantItem(SidebarWidth).Column(ComposeSidebar);
                });
            });
        });
    }

    private void ContactChip(RowDescriptor row, string svg, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        row.AutoItem().PaddingRight(12).Background(Navy).PaddingVertical(4).PaddingHorizontal(8).Row(r =>
        {
            r.AutoItem().Width(10).Height(10).Svg(svg);
            r.AutoItem().PaddingLeft(6).Text(value).FontSize(8).Bold().FontColor(White);
        });
    }

    private void ComposeMainContent(ColumnDescriptor col)
    {
        SectionTitle(col, "WORK EXPERIENCE");
        foreach (var exp in _data.Experience)
        {
            col.Item().PaddingBottom(18).Row(row =>
            {
                row.ConstantItem(DateColWidth).Column(dateCol =>
                {
                    dateCol.Item().Text(exp.Start).FontSize(8.5f).Bold();
                    dateCol.Item().PaddingTop(1).Text(exp.End ?? "PRESENT").FontSize(7).FontColor(Faint);
                });

                row.RelativeItem().Column(descCol =>
                {
                    descCol.Item().Text(exp.Role).FontSize(11).Bold();
                    descCol.Item().Text(exp.Company).FontSize(10).SemiBold().FontColor(NavyMid);
                    if (exp.Bullets is { Count: > 0 })
                    {
                        descCol.Item().PaddingTop(4).Column(bulletsCol =>
                        {
                            foreach (var b in exp.Bullets)
                            {
                                bulletsCol.Item().PaddingBottom(2).Row(r =>
                                {
                                    r.AutoItem().PaddingRight(4).Text("•").FontSize(9f);
                                    r.RelativeItem().Text(b).FontSize(9f).LineHeight(1.4f);
                                });
                            }
                        });
                    }
                });
            });
        }

        if (_data.Projects.Any())
        {
            SectionTitle(col, "SELECTED PROJECTS");
            foreach (var proj in _data.Projects)
            {
                col.Item().PaddingBottom(14).Column(pc =>
                {
                    pc.Item().Text(proj.Title).FontSize(10.5f).Bold();
                    if (!string.IsNullOrWhiteSpace(proj.Description))
                        pc.Item().PaddingTop(2).Text(proj.Description).LineHeight(1.4f);
                });
            }
        }
    }

    private void ComposeSidebar(ColumnDescriptor col)
    {
        SectionTitle(col, "CORE SKILLS");
        col.Item().Column(sc =>
        {
            foreach (var skill in _data.Skills)
            {
                sc.Item().PaddingBottom(8).Column(skillCol =>
                {
                    skillCol.Item().Row(r =>
                    {
                        r.RelativeItem().Text(skill.Name).FontSize(8.5f).SemiBold();
                        if (skill.IsMatched)
                            r.AutoItem().Text("★").FontSize(8).FontColor(NavyMid);
                    });
                    skillCol.Item().PaddingTop(2).Height(3).Background(RuleLight).Row(r =>
                    {
                        r.RelativeItem(skill.Level / 10f).Background(Navy);
                        r.RelativeItem((100 - skill.Level) / 10f);
                    });
                });
            }
        });

        SectionTitle(col, "EDUCATION", 24);
        foreach (var edu in _data.Education)
        {
            col.Item().PaddingBottom(10).Column(ec =>
            {
                ec.Item().Text(edu.Degree).FontSize(9).Bold();
                ec.Item().Text(edu.Institution).FontSize(8.5f).FontColor(NavyMid);
                ec.Item().Text(edu.Year).FontSize(8).FontColor(Faint);
            });
        }

        if (_data.Languages.Any())
        {
            SectionTitle(col, "LANGUAGES", 24);
            col.Item().Text(string.Join(" • ", _data.Languages)).FontSize(8.5f).LineHeight(1.4f);
        }
    }

    private void SectionTitle(ColumnDescriptor col, string title, float paddingTop = 0)
    {
        col.Item().PaddingTop(paddingTop).PaddingBottom(8).Column(c =>
        {
            c.Item().Text(title).FontSize(9).ExtraBold().LetterSpacing(0.1f);
            c.Item().PaddingTop(2).Height(2).Width(20).Background(Navy);
        });
    }

    private string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}" : parts[0][0].ToString();
    }
}
