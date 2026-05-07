using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CvManagementApp.Models;

namespace CvManagementApp.Templates;

/// <summary>
/// Exact replica of the "Jane Huang" orange/salmon CV design.
///
/// Page is divided into 4 quadrants:
///
///  ┌─────────────────────┬──────────────────────────────────────┐
///  │  TOP-LEFT           │  TOP-RIGHT                          │
///  │  Salmon #F47B63     │  Peach #FCDDD6                      │
///  │  • Orange avatar    │  • RESUME OBJECTIVE heading         │
///  │  • Big bold name    │  • Contact rows     │                                     │
///  ├─────────────────────┴──────────────────────────────────────┤
///  │  HORIZONTAL DIVIDER  (light gray strip)                   │
///  ├─────────────────────┬──────────────────────────────────────┤
///  │  BOTTOM-LEFT        │  BOTTOM-RIGHT                       │
///  │  White #FFFFFF      │  White #FFFFFF                      │
///  │  • EDUCATION        │  • WORK HISTORY                     │
///  │  • SKILLS + bars    │  • ACCOMPLISHMENTS                  │
///  └─────────────────────┴──────────────────────────────────────┘
///
///  All measurements and colours taken directly from the reference image.
/// </summary>
public class ExecutiveCvDocument : IDocument
{
    private readonly CvData _data;

    // ── Font ──────────────────────────────────────────────────────
    private const string Font           = "Nunito Sans";

    // ── Palette ───────────────────────────────────────────────────
    private const string Salmon         = "#F47B63";   // top-left bg
    private const string OrangeDark     = "#D95E43";   // avatar box + skill fill + name rule
    private const string Peach          = "#FCDDD6";   // top-right bg
    private const string DividerGray    = "#E8E8E8";   // horizontal divider strip
    private const string White          = "#FFFFFF";   // bottom halves
    private const string IconBg         = "#00000028"; // translucent circle on salmon
    private const string TextDark       = "#1A1A1A";   // all headings + name
    private const string TextBody       = "#333333";   // body copy
    private const string TextMuted      = "#666666";   // dates, location, italic lines
    private const string SkillBarBg     = "#E8E8E8";   // unfilled bar portion

    // ── Geometry ──────────────────────────────────────────────────
    private const float SidebarW        = 210f;    // left column width
    private const float AvatarBoxSize   = 62f;     // orange initials square
    private const float TopHalfH        = 230f;    // height of top two quadrants
    private const float DividerH        = 6f;      // gray strip height
    private const float SidebarPad      = 20f;     // horizontal padding in sidebar
    private const float ContentPad      = 24f;     // horizontal padding in right col
    private const float TopPad          = 26f;     // top padding in all quadrants

    // Contact icon circle size
    private const float IconSize        = 20f;

    // SVG icons (white fill)
    private const string SvgEmail    = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='white'><path d='M20 4H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z'/></svg>";
    private const string SvgPhone    = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='white'><path d='M6.6 10.8c1.4 2.8 3.8 5.1 6.6 6.6l2.2-2.2c.3-.3.7-.4 1-.2 1.1.4 2.3.6 3.6.6.6 0 1 .4 1 1V20c0 .6-.4 1-1 1C10.6 21 3 13.4 3 4c0-.6.4-1 1-1h3.5c.6 0 1 .4 1 1 0 1.3.2 2.5.6 3.6.1.3 0 .7-.5 1.2z'/></svg>";
    private const string SvgLocation = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='white'><path d='M12 2C8.1 2 5 5.1 5 9c0 5.3 7 13 7 13s7-7.8 7-13c0-3.9-3.1-7-7-7zm0 9.5c-1.4 0-2.5-1.1-2.5-2.5S10.6 6.5 12 6.5s2.5 1.1 2.5 2.5S13.4 11.5 12 11.5z'/></svg>";

    public ExecutiveCvDocument(CvData data) => _data = data;
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    // ═══════════════════════════════════════════════════════════════
    //  PAGE COMPOSE
    // ═══════════════════════════════════════════════════════════════
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(x => x
                .FontFamily(Font)
                .FontSize(9f)
                .FontColor(TextBody));

            page.Content().Column(root =>
            {
                // ── ROW 1: top two quadrants ───────────────────────
                root.Item().Height(TopHalfH).Row(topRow =>
                {
                    // TOP-LEFT: salmon
                    topRow.ConstantItem(SidebarW)
                          .Background(Salmon)
                          .PaddingTop(TopPad)
                          .PaddingHorizontal(SidebarPad)
                          .Column(topLeft =>
                          {
                              ComposeTopLeft(topLeft);
                          });

                    // TOP-RIGHT: peach
                    topRow.RelativeItem()
                          .Background(Peach)
                          .PaddingTop(TopPad)
                          .PaddingHorizontal(ContentPad)
                          .Column(topRight =>
                          {
                              ComposeTopRight(topRight);
                          });
                });

                // ── ROW 2: horizontal divider ──────────────────────
                root.Item().Height(DividerH).Background(DividerGray);

                // ── ROW 3: bottom two quadrants ────────────────────
                root.Item().Row(botRow =>
                {
                    // BOTTOM-LEFT: white, Education + Skills
                    botRow.ConstantItem(SidebarW)
                          .Background(White)
                          .BorderRight(0.5f).BorderColor(DividerGray)
                          .PaddingTop(20)
                          .PaddingHorizontal(SidebarPad)
                          .PaddingBottom(36)
                          .Column(botLeft =>
                          {
                              ComposeBottomLeft(botLeft);
                          });

                    // BOTTOM-RIGHT: white, Work History + Accomplishments
                    botRow.RelativeItem()
                          .Background(White)
                          .PaddingTop(20)
                          .PaddingHorizontal(ContentPad)
                          .PaddingBottom(36)
                          .Column(botRight =>
                          {
                              ComposeBottomRight(botRight);
                          });
                });
            });
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  TOP-LEFT  —  salmon: avatar · name · contacts
    // ═══════════════════════════════════════════════════════════════
    private void ComposeTopLeft(ColumnDescriptor col)
    {
        // Orange avatar box, left-aligned (matching the photo)
        col.Item().AlignLeft()
           .Width(AvatarBoxSize).Height(AvatarBoxSize)
           .Background(OrangeDark)
           .AlignCenter().AlignMiddle()
           .Text(GetInitials(_data.Candidate.Name))
           .FontFamily(Font).FontSize(20).Bold().FontColor(White);

        // Name — large bold uppercase
        col.Item().PaddingTop(14)
           .Text(_data.Candidate.Name.ToUpperInvariant())
           .FontFamily(Font).FontSize(22).Bold().FontColor(TextDark)
           .LineHeight(1.05f);

        // Short orange rule under name
        col.Item().PaddingTop(7).PaddingBottom(12)
           .Width(26).Height(3).Background(OrangeDark);

        // Contact rows
        ContactRow(col, SvgEmail,    _data.Candidate.Email);
        ContactRow(col, SvgPhone,    _data.Candidate.Phone);
        ContactRow(col, SvgLocation, _data.Candidate.Location);
    }

    private static void ContactRow(ColumnDescriptor col, string svg, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        col.Item().PaddingBottom(9).Row(row =>
        {
            // Circle icon
            row.AutoItem().AlignMiddle().PaddingRight(9)
               .Width(IconSize).Height(IconSize)
               .Background(IconBg)
               .AlignCenter().AlignMiddle()
               .Padding(4).Svg(svg);

            row.RelativeItem().AlignMiddle()
               .Text(value).FontSize(9f).Bold().FontColor(TextDark);
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  TOP-RIGHT  —  peach: Resume Objective
    // ═══════════════════════════════════════════════════════════════
    private void ComposeTopRight(ColumnDescriptor col)
    {
        // Section heading
        SectionHeading(col, "Resume Objective", "#E8C8C0");

        // Summary text
        if (!string.IsNullOrWhiteSpace(_data.Summary))
        {
            col.Item().PaddingTop(10).Text(_data.Summary)
               .FontSize(9.5f).FontColor(TextBody).LineHeight(1.65f);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  BOTTOM-LEFT  —  white: Education + Skills
    // ═══════════════════════════════════════════════════════════════
    private void ComposeBottomLeft(ColumnDescriptor col)
    {
        // ── Education ─────────────────────────────────────────────
        SectionHeading(col, "Education", DividerGray);

        if (_data.Education is { Count: > 0 })
        {
            col.Item().PaddingTop(8).Column(edu =>
            {
                foreach (var e in _data.Education)
                    EducationItem(edu, e);
            });
        }

        // ── Skills ────────────────────────────────────────────────
        col.Item().PaddingTop(16).Column(skills =>
        {
            SectionHeading(skills, "Skills", DividerGray);

            if (_data.Skills is { Count: > 0 })
            {
                skills.Item().PaddingTop(6).Column(bars =>
                {
                    foreach (var skill in _data.Skills)
                        SkillBar(bars, skill);
                });
            }
        });
    }

    private static void EducationItem(ColumnDescriptor col, CvEducation edu)
    {
        col.Item().PaddingBottom(12).Column(e =>
        {
            // Institution name — bold
            e.Item().Text(edu.Institution)
                .FontSize(9.5f).Bold().FontColor(TextDark);

            // Date — small muted
            var meta = BuildMeta(null, edu.Year);
            if (!string.IsNullOrWhiteSpace(meta))
                e.Item().PaddingTop(1).Text(meta)
                    .FontSize(8f).FontColor(TextMuted);

            // Degree in bold italic
            e.Item().PaddingTop(3).Text(edu.Degree)
                .FontSize(8.5f).Bold().Italic().FontColor(TextDark)
                .LineHeight(1.3f);

        });
    }

    private static void SkillBar(ColumnDescriptor col, string skill)
    {
        // Support "SkillName:level" (1–5). Default = 4.
        var parts = skill.Split(':');
        var name  = parts[0].Trim();
        int level = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var l) ? l : 4;
        float pct = Math.Clamp(level / 5f, 0.05f, 1f);

        col.Item().PaddingBottom(9).Column(s =>
        {
            s.Item().PaddingBottom(3).Text(name)
                .FontSize(9f).FontColor(TextDark);

            // Bar: orange fill + gray remainder
            s.Item().Height(5f).Row(bar =>
            {
                bar.RelativeItem(pct).Background(OrangeDark);
                if (pct < 1f)
                    bar.RelativeItem(1f - pct).Background(SkillBarBg);
            });
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  BOTTOM-RIGHT  —  white: Work History + Accomplishments
    // ═══════════════════════════════════════════════════════════════
    private void ComposeBottomRight(ColumnDescriptor col)
    {
        // ── Work History ──────────────────────────────────────────
        SectionHeading(col, "Work History", DividerGray);

        if (_data.Experience is { Count: > 0 })
        {
            col.Item().PaddingTop(8).Column(wh =>
            {
                foreach (var exp in _data.Experience)
                    ExperienceItem(wh, exp);
            });
        }

        // ── Accomplishments (Certifications list) ─────────────────
        if (_data.Certifications is { Count: > 0 })
        {
            col.Item().PaddingTop(16).Column(acc =>
            {
                SectionHeading(acc, "Accomplishments", DividerGray);
                acc.Item().PaddingTop(6).Column(list =>
                {
                    foreach (var cert in _data.Certifications)
                        BulletRow(list, cert);
                });
            });
        }

        // ── Optional: Projects ─────────────────────────────────────
        if (_data.Projects is { Count: > 0 })
        {
            col.Item().PaddingTop(16).Column(proj =>
            {
                SectionHeading(proj, "Projects", DividerGray);
                proj.Item().PaddingTop(8).Column(list =>
                {
                    foreach (var prj in _data.Projects)
                        ProjectItem(list, prj);
                });
            });
        }

        // ── Optional: Activities ───────────────────────────────────
        if (_data.Activities is { Count: > 0 })
        {
            col.Item().PaddingTop(16).Column(act =>
            {
                SectionHeading(act, "Activities", DividerGray);
                act.Item().PaddingTop(8).Column(list =>
                {
                    foreach (var a in _data.Activities)
                        ActivityItem(list, a);
                });
            });
        }

        // ── Optional: Languages ────────────────────────────────────
        if (_data.Languages is { Count: > 0 })
        {
            col.Item().PaddingTop(16).Column(lang =>
            {
                SectionHeading(lang, "Languages", DividerGray);
                lang.Item().PaddingTop(6)
                    .Text(string.Join("   ·   ", _data.Languages))
                    .FontSize(9f).FontColor(TextBody);
            });
        }
    }

    private static void ExperienceItem(ColumnDescriptor col, CvExperience exp)
    {
        col.Item().PaddingBottom(14).Column(item =>
        {
            // "Company – Role" on one line, both bold
            item.Item().Row(r =>
            {
                r.AutoItem().Text(exp.Company)
                    .FontSize(9.5f).Bold().FontColor(TextDark);
                r.AutoItem().Text(" – ")
                    .FontSize(9.5f).FontColor(TextMuted);
                r.AutoItem().Text(exp.Role)
                    .FontSize(9.5f).Bold().FontColor(TextDark);
            });

            // "Start – End" italic muted
            var dateStr = exp.End is not null
                ? $"{exp.Start} – {exp.End}"
                : $"{exp.Start} – Present";
            var meta = BuildMeta(null, dateStr);
            item.Item().PaddingBottom(5).Text(meta)
                .FontSize(8.5f).Italic().FontColor(TextMuted);

            // Bullets
            if (exp.Bullets is { Count: > 0 })
                item.Item().Column(bullets =>
                {
                    foreach (var b in exp.Bullets)
                        BulletRow(bullets, b);
                });
        });
    }

    private static void ProjectItem(ColumnDescriptor col, CvProject prj)
    {
        col.Item().PaddingBottom(10).Column(p =>
        {
            p.Item().Text(prj.Title).FontSize(9.5f).Bold().FontColor(TextDark);
            if (!string.IsNullOrWhiteSpace(prj.Description))
                p.Item().PaddingTop(1).PaddingBottom(4).Text(prj.Description)
                    .FontSize(8.5f).Italic().FontColor(TextMuted).LineHeight(1.5f);
            if (prj.Bullets is { Count: > 0 })
                p.Item().Column(bl =>
                {
                    foreach (var b in prj.Bullets) BulletRow(bl, b);
                });
        });
    }

    private static void ActivityItem(ColumnDescriptor col, CvActivity act)
    {
        col.Item().PaddingBottom(8).Column(a =>
        {
            a.Item().Row(r =>
            {
                r.AutoItem().Text(act.Title).FontSize(9.5f).Bold().FontColor(TextDark);
                if (!string.IsNullOrWhiteSpace(act.Role))
                    r.AutoItem().Text($"  —  {act.Role}")
                        .FontSize(9f).Italic().FontColor(TextMuted);
            });
            if (!string.IsNullOrWhiteSpace(act.Description))
                a.Item().PaddingTop(3).Text(act.Description)
                    .FontSize(9f).FontColor(TextBody).LineHeight(1.55f);
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  SHARED HELPERS
    // ═══════════════════════════════════════════════════════════════

    /// Bold uppercase heading + thin horizontal rule.
    private static void SectionHeading(ColumnDescriptor col, string title, string ruleColor)
    {
        col.Item().Column(h =>
        {
            h.Item().Text(title.ToUpperInvariant())
                .FontSize(8f).Bold().FontColor(TextDark)
                .LetterSpacing(1.3f);
            h.Item().PaddingTop(3).LineHorizontal(1f).LineColor(ruleColor);
        });
    }

    /// Bullet point: "·" + text
    private static void BulletRow(ColumnDescriptor col, string text)
    {
        col.Item().PaddingBottom(4).Row(row =>
        {
            row.AutoItem().PaddingRight(8).Text("·")
                .FontSize(11f).FontColor(TextMuted);
            row.RelativeItem().Text(text)
                .FontSize(9f).FontColor(TextBody).LineHeight(1.5f);
        });
    }

    /// Build "Location · Date" or just date if no location.
    private static string BuildMeta(string? location, string? date)
    {
        if (string.IsNullOrWhiteSpace(location)) return date ?? "";
        if (string.IsNullOrWhiteSpace(date))     return location;
        return $"{location}  ·  {date}";
    }

    /// Extract two initials from a full name.
    private static string GetInitials(string name)
    {
        var parts = name.Trim().Split(' ',
            StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name[..Math.Min(2, name.Length)].ToUpperInvariant();
    }
}
