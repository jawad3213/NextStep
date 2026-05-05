using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NextStep.Modules.Cv.Models;

namespace NextStep.Modules.Cv.Templates;

/// <summary>
/// Executive CV — four-quadrant salmon/peach/white layout.
/// </summary>
public class ExecutiveCvDocument : IDocument
{
    private readonly CvData _data;
    public ExecutiveCvDocument(CvData data) => _data = data;

    private string Font => _data.FontFamily ?? "Nunito Sans";
    private string Salmon => _data.ThemeColor ?? "#F47B63";
    private string OrangeDark => _data.ThemeColor ?? "#D95E43";

    private const string Peach       = "#FCDDD6";
    private const string DividerGray = "#E8E8E8";
    private const string White       = "#FFFFFF";
    private const string IconBg      = "#00000028";
    private const string TextDark    = "#1A1A1A";
    private const string TextBody    = "#333333";
    private const string TextMuted   = "#666666";
    private const string SkillBarBg  = "#E8E8E8";

    private const float SidebarW      = 210f;
    private const float AvatarBoxSize = 62f;
    private const float TopHalfH      = 230f;
    private const float DividerH      = 6f;
    private const float SidebarPad    = 20f;
    private const float ContentPad    = 24f;
    private const float TopPad        = 26f;
    private const float IconSize      = 20f;

    private const string SvgEmail    = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='white'><path d='M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z'/></svg>";
    private const string SvgPhone    = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='white'><path d='M6.6 10.8c1.4 2.8 3.8 5.1 6.6 6.6l2.2-2.2c.3-.3.7-.4 1-.2 1.1.4 2.3.6 3.6.6.6 0 1 .4 1 1V20c0 .6-.4 1-1 1C10.6 21 3 13.4 3 4c0-.6.4-1 1-1h3.5c.6 0 1 .4 1 1 0 1.3.2 2.5.6 3.6.1.3 0 .7-.5 1.2z'/></svg>";
    private const string SvgLocation = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='white'><path d='M12 2C8.1 2 5 5.1 5 9c0 5.3 7 13 7 13s7-7.8 7-13c0-3.9-3.1-7-7-7zm0 9.5c-1.4 0-2.5-1.1-2.5-2.5S10.6 6.5 12 6.5s2.5 1.1 2.5 2.5S13.4 11.5 12 11.5z'/></svg>";

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(x => x.FontFamily(Font).FontSize(9f).FontColor(TextBody));

            page.Content().Column(root =>
            {
                root.Item().Height(TopHalfH).Row(topRow =>
                {
                    topRow.ConstantItem(SidebarW).Background(Salmon)
                          .PaddingTop(TopPad).PaddingHorizontal(SidebarPad)
                          .Column(ComposeTopLeft);
                    topRow.RelativeItem().Background(Peach)
                          .PaddingTop(TopPad).PaddingHorizontal(ContentPad)
                          .Column(ComposeTopRight);
                });

                root.Item().Height(DividerH).Background(DividerGray);

                root.Item().Row(botRow =>
                {
                    botRow.ConstantItem(SidebarW).Background(White)
                          .BorderRight(0.5f).BorderColor(DividerGray)
                          .PaddingTop(20).PaddingHorizontal(SidebarPad).PaddingBottom(36)
                          .Column(ComposeBottomLeft);

                    botRow.RelativeItem().Background(White)
                          .PaddingTop(20).PaddingHorizontal(ContentPad).PaddingBottom(36)
                          .Column(ComposeBottomRight);
                });
            });
        });
    }

    private void ComposeTopLeft(ColumnDescriptor col)
    {
        col.Item().AlignLeft().Width(AvatarBoxSize).Height(AvatarBoxSize)
           .Background(OrangeDark).AlignCenter().AlignMiddle()
           .Text(GetInitials(_data.Candidate.Name))
           .FontFamily(Font).FontSize(20).Bold().FontColor(White);

        col.Item().PaddingTop(14).Text(_data.Candidate.Name.ToUpperInvariant())
           .FontFamily(Font).FontSize(22).Bold().FontColor(TextDark).LineHeight(1.05f);

        col.Item().PaddingTop(7).PaddingBottom(12).Width(26).Height(3).Background(OrangeDark);

        ContactRow(col, SvgEmail,    _data.Candidate.Email);
        ContactRow(col, SvgPhone,    _data.Candidate.Phone);
        ContactRow(col, SvgLocation, _data.Candidate.Location);
    }

    private static void ContactRow(ColumnDescriptor col, string svg, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().PaddingBottom(9).Row(row =>
        {
            row.AutoItem().AlignMiddle().PaddingRight(9)
               .Width(IconSize).Height(IconSize).Background(IconBg)
               .AlignCenter().AlignMiddle().Padding(4).Svg(svg);
            row.RelativeItem().AlignMiddle().Text(value).FontSize(9f).Bold().FontColor(TextDark);
        });
    }

    private void ComposeTopRight(ColumnDescriptor col)
    {
        SectionHeading(col, "Resume Objective", "#E8C8C0");
        if (!string.IsNullOrWhiteSpace(_data.Summary))
            col.Item().PaddingTop(10).Text(_data.Summary).FontSize(9.5f).FontColor(TextBody).LineHeight(1.65f);
    }

    private void ComposeBottomLeft(ColumnDescriptor col)
    {
        SectionHeading(col, "Core Skills", "#F0F0F0");
        col.Item().PaddingTop(12).Column(c =>
        {
            foreach (var skill in _data.Skills)
            {
                c.Item().PaddingBottom(8).Column(sc =>
                {
                    sc.Item().Row(r =>
                    {
                        r.RelativeItem().Text(skill.Name).FontSize(8.5f).SemiBold().FontColor(TextDark);
                        if (skill.IsMatched)
                            r.AutoItem().Text(" MATCHED").FontSize(6).Bold().FontColor(OrangeDark);
                    });
                    sc.Item().PaddingTop(3).Height(4).Background(SkillBarBg).Row(r =>
                    {
                        r.RelativeItem(skill.Level / 10f).Background(OrangeDark);
                        r.RelativeItem((100 - skill.Level) / 10f);
                    });
                });
            }
        });

        col.Item().PaddingTop(SectionGap).Column(c =>
        {
            SectionHeading(c, "Education", "#F0F0F0");
            foreach (var edu in _data.Education)
            {
                c.Item().PaddingTop(10).Column(ec =>
                {
                    ec.Item().Text(edu.Institution).FontSize(9f).Bold().FontColor(TextDark);
                    ec.Item().Text(edu.Degree).FontSize(8.5f).Italic().FontColor(TextBody);
                    ec.Item().Text($"{edu.StartDate:MMM yyyy} - {(edu.EndDate.HasValue ? edu.EndDate.Value.ToString("MMM yyyy") : "Present")}").FontSize(8f).FontColor(TextMuted);
                });
            }
        });

        col.Item().PaddingTop(SectionGap).Column(c =>
        {
            SectionHeading(c, "Languages", "#F0F0F0");
            foreach (var lang in _data.Languages)
                c.Item().PaddingTop(6).Text(lang).FontSize(8.5f).FontColor(TextBody);
        });
    }

    private void ComposeBottomRight(ColumnDescriptor col)
    {
        SectionHeading(col, "Work Experience", "#F0F0F0");
        foreach (var exp in _data.Experience)
        {
            col.Item().PaddingTop(12).Column(c =>
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text(exp.Position).FontSize(10f).Bold().FontColor(TextDark);
                    r.AutoItem().Text($"{exp.StartDate:MMM yyyy} - {(exp.EndDate.HasValue ? exp.EndDate.Value.ToString("MMM yyyy") : "Present")}").FontSize(8.5f).FontColor(TextMuted);
                });
                c.Item().Text(exp.Company).FontSize(9.5f).SemiBold().FontColor(OrangeDark);
                if (!string.IsNullOrWhiteSpace(exp.Description))
                    c.Item().PaddingTop(4).Text(exp.Description).FontSize(9f).FontColor(TextBody).LineHeight(1.5f);
            });
        }

        if (_data.Projects.Any())
        {
            col.Item().PaddingTop(SectionGap).Column(c =>
            {
                SectionHeading(c, "Key Projects", "#F0F0F0");
                foreach (var proj in _data.Projects)
                {
                    c.Item().PaddingTop(10).Column(pc =>
                    {
                        pc.Item().Text(proj.Title).FontSize(9.5f).Bold().FontColor(TextDark);
                        if (!string.IsNullOrWhiteSpace(proj.Description))
                            pc.Item().PaddingTop(2).Text(proj.Description).FontSize(9f).FontColor(TextBody);
                    });
                }
            });
        }
    }

    private void SectionHeading(ColumnDescriptor col, string title, string bgColor)
    {
        col.Item().Background(bgColor).PaddingVertical(4).PaddingHorizontal(8)
           .Text(title.ToUpperInvariant()).FontFamily(Font).FontSize(9).Bold().FontColor(TextDark).LetterSpacing(0.05f);
    }

    private string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[1][0]}" : parts[0][0].ToString();
    }

    private const float SectionGap = 20f;
}
