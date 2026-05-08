using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NextStep.Modules.Cv.Models;

namespace NextStep.Modules.Cv.Templates;

/// <summary>
/// Two-column CV — dark navy sidebar with skill bars + white content column.
/// Elegant / "Alex Mercer" style.
/// </summary>
public class ElegantCvDocument : IDocument
{
    private readonly CvData _data;

    private string Font => _data.FontFamily ?? "Lato";
    private string NavyDark => _data.ThemeColor ?? "#1E2A45";
    private string NavyMid => _data.ThemeColor ?? "#2E3F60"; // Using ThemeColor for both primary accents
    private string HeadingColor => _data.ThemeColor ?? "#1E2A45";

    private const string White        = "#FFFFFF";
    private const string WhiteMuted   = "#C8D0DE";
    private const string WhiteFaint   = "#8A95A8";
    private const string BodyColor    = "#2E2E2E";
    private const string MutedColor   = "#666666";
    private const string RuleColor    = "#CBD2DF";

    private const float SidebarW    = 168f;
    private const float HeaderH     = 112f;
    private const float PhotoSize   = 74f;
    private const float SidebarPadH = 16f;
    private const float ContentPadH = 22f;
    private const float SectionGap  = 14f;

    public ElegantCvDocument(CvData data) => _data = data;
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(x => x.FontFamily(Font).FontSize(9f).FontColor(BodyColor));

            page.Content().Column(root =>
            {
                // Full-width header
                root.Item().Height(HeaderH).Background(NavyDark).Row(row =>
                {
                    row.ConstantItem(SidebarW).AlignMiddle().AlignCenter()
                       .Width(PhotoSize).Height(PhotoSize)
                       .Background(NavyMid).AlignCenter().AlignMiddle()
                       .Text("📷").FontSize(22);

                    row.ConstantItem(1).Background("#FFFFFF18");
                    row.ConstantItem(ContentPadH);

                    row.RelativeItem().AlignMiddle().Column(n =>
                    {
                        n.Item().Text(_data.Candidate.Name.ToUpperInvariant())
                         .FontSize(24).Bold().FontColor(White).LetterSpacing(0.12f);

                        if (!string.IsNullOrWhiteSpace(_data.Candidate.Location))
                            n.Item().PaddingTop(6)
                             .Text(_data.Candidate.Location.ToUpperInvariant())
                             .FontSize(8.5f).FontColor(WhiteMuted).LetterSpacing(0.1f);
                    });

                    row.ConstantItem(ContentPadH);
                });

                // Two-column body
                root.Item().Row(body =>
                {
                    body.ConstantItem(SidebarW)
                        .Background(NavyDark)
                        .PaddingHorizontal(SidebarPadH)
                        .PaddingTop(18).PaddingBottom(40)
                        .Column(sidebar =>
                        {
                            SidebarContact(sidebar);
                            SidebarSkills(sidebar);
                            SidebarLanguages(sidebar);
                            SidebarEducation(sidebar);
                        });

                    body.RelativeItem()
                        .Background(White)
                        .PaddingHorizontal(ContentPadH)
                        .PaddingTop(18).PaddingBottom(40)
                        .Column(content =>
                        {
                            ContentSummary(content);
                            ContentExperience(content);
                            ContentProjects(content);
                            ContentCertifications(content);
                            ContentActivities(content);
                        });
                });
            });
        });
    }

    private static void SidebarHeading(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(SectionGap).Column(h =>
        {
            h.Item().Text(title.ToUpperInvariant()).FontSize(8f).Bold().FontColor(White).LetterSpacing(0.1f);
            h.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor("#FFFFFF35");
        });
        col.Item().PaddingBottom(8);
    }

    private void SidebarContact(ColumnDescriptor col)
    {
        SidebarHeading(col, "Contact");
        SidebarContactRow(col, "✉", _data.Candidate.Email);
        SidebarContactRow(col, "☎", _data.Candidate.Phone);
        SidebarContactRow(col, "⌂", _data.Candidate.Location);
    }

    private static void SidebarContactRow(ColumnDescriptor col, string icon, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().PaddingBottom(6).Row(row =>
        {
            row.AutoItem().PaddingRight(7).Text(icon).FontSize(9.5f).FontColor(WhiteMuted);
            row.RelativeItem().Text(value).FontSize(8.5f).FontColor(WhiteMuted).LineHeight(1.4f);
        });
    }

    private void SidebarSkills(ColumnDescriptor col)
    {
        if (_data.Skills is not { Count: > 0 }) return;
        SidebarHeading(col, "Skills");
        foreach (var skill in _data.Skills)
        {
            col.Item().PaddingBottom(8).Column(s =>
            {
                if (skill.IsMatched)
                    s.Item().Text(skill.Name).FontSize(8.5f).FontColor(White).Bold();
                else
                    s.Item().Text(skill.Name).FontSize(8.5f).FontColor(White);
                s.Item().PaddingTop(3).Row(bar =>
                {
                    bar.RelativeItem(skill.Level).Height(2.5f).Background(White);
                    if (skill.Level < 5) bar.RelativeItem(5 - skill.Level).Height(2.5f).Background("#FFFFFF28");
                });
            });
        }
    }

    private void SidebarLanguages(ColumnDescriptor col)
    {
        if (_data.Languages is not { Count: > 0 }) return;
        SidebarHeading(col, "Languages");
        foreach (var lang in _data.Languages)
            col.Item().PaddingBottom(5).Text(lang).FontSize(8.5f).FontColor(WhiteMuted);
    }

    private void SidebarEducation(ColumnDescriptor col)
    {
        if (_data.Education is not { Count: > 0 }) return;
        SidebarHeading(col, "Education");
        foreach (var edu in _data.Education)
            col.Item().PaddingBottom(10).Column(e =>
            {
                e.Item().Text(edu.Degree).FontSize(8.5f).Bold().FontColor(White).LineHeight(1.35f);
                e.Item().PaddingTop(2).Text(edu.Institution).FontSize(8f).Italic().FontColor(WhiteMuted);
                e.Item().PaddingTop(1).Text(edu.Year).FontSize(7.5f).FontColor(WhiteFaint);
            });
    }

    private static void ContentHeading(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(SectionGap).Column(h =>
        {
            h.Item().Text(title.ToUpperInvariant()).FontSize(8.5f).Bold().FontColor(HeadingColor).LetterSpacing(0.1f);
            h.Item().PaddingTop(3).LineHorizontal(0.8f).LineColor(RuleColor);
        });
        col.Item().PaddingBottom(8);
    }

    private void ContentSummary(ColumnDescriptor col)
    {
        if (string.IsNullOrWhiteSpace(_data.Summary)) return;
        ContentHeading(col, "Professional Summary");
        col.Item().PaddingBottom(SectionGap).Text(_data.Summary).FontSize(9f).FontColor(BodyColor).LineHeight(1.65f);
    }

    private void ContentExperience(ColumnDescriptor col)
    {
        if (_data.Experience is not { Count: > 0 }) return;
        ContentHeading(col, "Work Experience");
        for (int i = 0; i < _data.Experience.Count; i++)
        {
            var exp  = _data.Experience[i];
            bool last = i == _data.Experience.Count - 1;
            col.Item().PaddingBottom(last ? 0 : 3).Column(item =>
            {
                item.Item().Row(r =>
                {
                    r.RelativeItem().Text(exp.Role).FontSize(11f).Bold().FontColor(HeadingColor);
                    var dateStr = exp.End is not null ? $"{exp.Start} – {exp.End}" : $"{exp.Start} – Present";
                    r.AutoItem().Text(dateStr).FontSize(8.5f).Italic().FontColor(MutedColor);
                });
                item.Item().PaddingTop(1).PaddingBottom(5).Text(exp.Company).FontSize(9f).Italic().FontColor(MutedColor);
                if (exp.Bullets is { Count: > 0 })
                    item.Item().Column(bullets => { foreach (var b in exp.Bullets) ArrowBullet(bullets, b); });
            });
            if (!last) col.Item().PaddingVertical(8).LineHorizontal(0.4f).LineColor(RuleColor);
        }
    }

    private void ContentProjects(ColumnDescriptor col)
    {
        if (_data.Projects is not { Count: > 0 }) return;
        ContentHeading(col, "Projects");
        foreach (var prj in _data.Projects)
            col.Item().PaddingBottom(10).Column(item =>
            {
                item.Item().Text(prj.Title).FontSize(11f).Bold().FontColor(HeadingColor);
                if (!string.IsNullOrWhiteSpace(prj.Description))
                    item.Item().PaddingTop(2).PaddingBottom(5).Text(prj.Description).FontSize(9f).Italic().FontColor(MutedColor).LineHeight(1.5f);
                if (prj.Bullets is { Count: > 0 })
                    item.Item().Column(bullets => { foreach (var b in prj.Bullets) ArrowBullet(bullets, b); });
            });
    }

    private void ContentCertifications(ColumnDescriptor col)
    {
        if (_data.Certifications is not { Count: > 0 }) return;
        ContentHeading(col, "Certifications");
        foreach (var cert in _data.Certifications)
            col.Item().PaddingBottom(4).Row(row =>
            {
                row.AutoItem().PaddingRight(6).Text("▸").FontSize(9f).FontColor(HeadingColor);
                row.RelativeItem().Text(cert).FontSize(9f).FontColor(BodyColor).LineHeight(1.4f);
            });
    }

    private void ContentActivities(ColumnDescriptor col)
    {
        if (_data.Activities is not { Count: > 0 }) return;
        ContentHeading(col, "Activities");
        foreach (var act in _data.Activities)
            col.Item().PaddingBottom(8).Column(item =>
            {
                item.Item().Row(r =>
                {
                    r.AutoItem().Text(act.Title).FontSize(11f).Bold().FontColor(HeadingColor);
                    if (!string.IsNullOrWhiteSpace(act.Role))
                        r.AutoItem().Text($"  —  {act.Role}").FontSize(9f).Italic().FontColor(MutedColor);
                });
                if (!string.IsNullOrWhiteSpace(act.Description))
                    item.Item().PaddingTop(3).Text(act.Description).FontSize(9f).FontColor(BodyColor).LineHeight(1.55f);
            });
    }

    private static void ArrowBullet(ColumnDescriptor col, string text)
    {
        col.Item().PaddingBottom(3).Row(row =>
        {
            row.AutoItem().PaddingRight(6).Text("▸").FontSize(8.5f).FontColor(BodyColor);
            row.RelativeItem().Text(text).FontSize(9f).FontColor(BodyColor).LineHeight(1.5f);
        });
    }
}
