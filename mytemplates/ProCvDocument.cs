using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CvManagementApp.Models;

namespace CvManagementApp.Templates;

/// <summary>
/// Polished Pro CV — two-column layout:
///   Left sidebar  : Skills (bar chart), Languages, Education, Certifications
///   Right column  : Work History, Projects, Activities
/// Header: photo + serif name + inline contact chips
/// A full-width tinted summary band separates header from body.
/// </summary>
public class ProCvDocument : IDocument
{
    private readonly CvData _data;

    // ── Typeface ──────────────────────────────────────────────────
    private const string SerifFont    = "Georgia";   // name block (premium serif feel)
    private const string SansFont     = "Inter";     // everything else (sleek sans-serif)

    // ── Palette ───────────────────────────────────────────────────
    private const string Navy         = "#1C2440";  // primary ink / sidebar accent
    private const string NavyMid      = "#4A5270";  // body text
    private const string Muted        = "#6B7492";  // labels / dates
    private const string Faint        = "#9198B0";  // tertiary
    private const string RuleLight    = "#E2E5EF";  // horizontal rules
    private const string SummaryBg    = "#F7F8FC";  // summary band tint
    private const string White        = "#FFFFFF";

    // ── Geometry ──────────────────────────────────────────────────
    private const float PageMargin    = 32f;
    private const float SidebarWidth  = 148f;
    private const float DateColWidth  = 70f;        // inside right column
    private const float PhotoW        = 78f;
    private const float PhotoH        = 88f;

    // ── SVG contact icons (white fill, rendered in Navy square) ───
    private const string SvgLocation = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='#ffffff'><path d='M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z'/></svg>";
    private const string SvgPhone    = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='#ffffff'><path d='M6.62 10.79c1.44 2.83 3.76 5.14 6.59 6.59l2.2-2.2c.27-.27.67-.36 1.02-.24 1.12.37 2.33.57 3.57.57.55 0 1 .45 1 1V20c0 .55-.45 1-1 1-9.39 0-17-7.61-17-17 0-.55.45-1 1-1h3.5c.55 0 1 .45 1 1 0 1.25.2 2.45.57 3.57.11.35.03.74-.25 1.02l-2.2 2.2z'/></svg>";
    private const string SvgEmail    = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='#ffffff'><path d='M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z'/></svg>";

    public ProCvDocument(CvData data) => _data = data;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    // ═══════════════════════════════════════════════════════════════
    //  Page layout
    // ═══════════════════════════════════════════════════════════════
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(PageMargin);
            page.DefaultTextStyle(x => x.FontFamily(SansFont).FontSize(9));

            page.Content().Column(col =>
            {
                // 1. Header band
                ComposeHeader(col);

                // 2. Summary band (full width, tinted)
                if (!string.IsNullOrWhiteSpace(_data.Summary))
                    ComposeSummaryBand(col);

                // 3. Two-column body
                col.Item().PaddingTop(4).Row(body =>
                {
                    // ── LEFT SIDEBAR ──────────────────────────────
                    body.ConstantItem(SidebarWidth).BorderRight(0.5f).BorderColor(RuleLight)
                        .PaddingRight(14).Column(left =>
                        {
                            ComposeSkills(left);
                            ComposeLanguages(left);
                            ComposeEducation(left);
                        });

                    // ── RIGHT COLUMN ──────────────────────────────
                    body.RelativeItem().PaddingLeft(20).Column(right =>
                    {
                        ComposeWorkHistory(right);

                        if (_data.Projects is { Count: > 0 })
                            ComposeProjects(right);

                        if (_data.Certifications is { Count: > 0 })
                            ComposeCertifications(right);

                        if (_data.Activities is { Count: > 0 })
                            ComposeActivities(right);
                    });
                });
            });
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  HEADER  — photo · name · contacts
    // ═══════════════════════════════════════════════════════════════
    private void ComposeHeader(ColumnDescriptor col)
    {
        col.Item().PaddingBottom(12).Row(row =>
        {
            // Photo placeholder
            row.ConstantItem(PhotoW).Height(PhotoH)
               .Background("#D8DCE8").AlignCenter().AlignMiddle()
               .Text("📷").FontSize(24);

            row.ConstantItem(18); // gap

            // Name + title + contacts
            row.RelativeItem().AlignMiddle().Column(nameCol =>
            {
                // Full name in serif
                nameCol.Item().Text(_data.Candidate.Name)
                    .FontFamily(SerifFont).FontSize(28).FontColor(Navy);

                // Job title / tagline
                if (!string.IsNullOrWhiteSpace(_data.Candidate.Location))
                {
                    nameCol.Item().PaddingTop(2).Text(_data.Candidate.Location.ToUpperInvariant())
                        .FontSize(8).Bold().FontColor(Muted).LetterSpacing(1.4f);
                }

                nameCol.Item().PaddingTop(10).Row(contacts =>
                {
                    ComposeContactChip(contacts, SvgLocation, _data.Candidate.Location);
                    ComposeContactChip(contacts, SvgPhone,    _data.Candidate.Phone);
                    ComposeContactChip(contacts, SvgEmail,    _data.Candidate.Email);
                });
            });
        });

        // Thick bottom rule under header
        col.Item().LineHorizontal(1.5f).LineColor(Navy);
    }

    // Contact chip: tiny icon square + label
    private static void ComposeContactChip(RowDescriptor row, string svg, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        row.AutoItem().PaddingRight(18).Row(chip =>
        {
            chip.AutoItem().AlignMiddle()
                .Width(14).Height(14).Background(Navy).Padding(3).Svg(svg);
            chip.AutoItem().PaddingLeft(4).AlignMiddle()
                .Text(value).FontSize(9).FontColor(NavyMid);
        });
    }

    // ═══════════════════════════════════════════════════════════════
    //  SUMMARY BAND
    // ═══════════════════════════════════════════════════════════════
    private void ComposeSummaryBand(ColumnDescriptor col)
    {
        col.Item()
           .Background(SummaryBg)
           .BorderBottom(0.5f).BorderColor(RuleLight)
           .Padding(11)
           .Text(_data.Summary)
           .FontSize(9).Italic().FontColor(NavyMid).LineHeight(1.65f);
    }

    // ═══════════════════════════════════════════════════════════════
    //  SIDEBAR SECTIONS
    // ═══════════════════════════════════════════════════════════════

    /// Sidebar section heading — all-caps, ruled underline
    private static void SidebarHeading(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(16).PaddingBottom(5).Column(h =>
        {
            h.Item().Text(title.ToUpperInvariant())
                .FontSize(8).Bold().FontColor(Navy).LetterSpacing(1.6f);
            h.Item().LineHorizontal(1.2f).LineColor(Navy);
        });
    }

    // ── Skills ───────────────────────────────────────────────────
    private void ComposeSkills(ColumnDescriptor col)
    {
        if (_data.Skills is not { Count: > 0 }) return;

        SidebarHeading(col, "Skills");

        foreach (var skill in _data.Skills)
        {
            // Support "SkillName:level" syntax where level is 1-5 (optional)
            var parts  = skill.Split(':');
            var name   = parts[0].Trim();
            int level  = parts.Length > 1 && int.TryParse(parts[1].Trim(), out var l) ? l : 4;
            float pct  = level / 5f;

            col.Item().PaddingBottom(7).Column(s =>
            {
                s.Item().Text(name).FontSize(9.5f).FontColor(Navy);
                s.Item().PaddingTop(3).Row(bar =>
                {
                    // Filled portion
                    bar.RelativeItem(pct).Height(2.5f).Background(Navy);
                    // Remainder
                    if (pct < 1f)
                        bar.RelativeItem(1f - pct).Height(2.5f).Background(RuleLight);
                });
            });
        }
    }

    // ── Languages ────────────────────────────────────────────────
    private void ComposeLanguages(ColumnDescriptor col)
    {
        if (_data.Languages is not { Count: > 0 }) return;

        SidebarHeading(col, "Languages");

        foreach (var lang in _data.Languages)
        {
            // Support "Language (Level)" or plain "Language"
            col.Item().PaddingBottom(5).Row(row =>
            {
                row.RelativeItem().Text(lang).FontSize(9.5f).FontColor(NavyMid);
            });
        }
    }

    // ── Education ────────────────────────────────────────────────
    private void ComposeEducation(ColumnDescriptor col)
    {
        if (_data.Education is not { Count: > 0 }) return;

        SidebarHeading(col, "Education");

        foreach (var edu in _data.Education)
        {
            col.Item().PaddingBottom(10).Column(e =>
            {
                e.Item().Text(edu.Degree)
                    .FontSize(9.5f).Bold().FontColor(Navy).LineHeight(1.3f);
                e.Item().PaddingTop(1).Text(edu.Institution)
                    .FontSize(9f).Italic().FontColor(Muted);
                e.Item().PaddingTop(1).Text(edu.Year)
                    .FontSize(8.5f).FontColor(Faint);
            });
        }
    }


    // ═══════════════════════════════════════════════════════════════
    //  RIGHT COLUMN SECTIONS
    // ═══════════════════════════════════════════════════════════════

    /// Right-column section heading
    private static void RightHeading(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(14).PaddingBottom(6).Column(h =>
        {
            h.Item().Text(title.ToUpperInvariant())
                .FontSize(8).Bold().FontColor(Navy).LetterSpacing(1.6f);
            h.Item().LineHorizontal(1.2f).LineColor(Navy);
        });
    }

    // ── Work History ─────────────────────────────────────────────
    private void ComposeWorkHistory(ColumnDescriptor col)
    {
        if (_data.Experience is not { Count: > 0 }) return;

        RightHeading(col, "Work History");

        for (int i = 0; i < _data.Experience.Count; i++)
        {
            var exp = _data.Experience[i];
            bool last = i == _data.Experience.Count - 1;

            var dateRange = exp.End is not null
                ? $"{exp.Start}\n{exp.End}"
                : $"{exp.Start}\nPresent";

            col.Item()
               .PaddingBottom(last ? 0 : 4)
               .BorderBottom(last ? 0 : 0.5f).BorderColor(RuleLight)
               .PaddingBottom(last ? 0 : 12)
               .Row(row =>
               {
                   // Date column
                   row.ConstantItem(DateColWidth).PaddingRight(10).Text(dateRange)
                       .FontSize(9).FontColor(Muted).LineHeight(1.35f);

                   // Details
                   row.RelativeItem().Column(d =>
                   {
                       d.Item().Text(exp.Role)
                           .FontSize(11).Bold().FontColor(Navy).LineHeight(1.2f);
                       d.Item().PaddingTop(1).Text(exp.Company)
                           .FontSize(9).Italic().FontColor(Muted);
                       if (exp.Bullets is { Count: > 0 })
                       {
                           d.Item().PaddingTop(5).Column(bullets =>
                           {
                               foreach (var b in exp.Bullets)
                                   ComposeBullet(bullets, b);
                           });
                       }
                   });
               });

            if (!last) col.Item().PaddingBottom(2);
        }
    }

    // ── Projects ─────────────────────────────────────────────────
    private void ComposeProjects(ColumnDescriptor col)
    {
        RightHeading(col, "Projects");

        for (int i = 0; i < _data.Projects!.Count; i++)
        {
            var prj  = _data.Projects[i];
            bool last = i == _data.Projects.Count - 1;

            col.Item()
               .PaddingBottom(last ? 0 : 4)
               .BorderBottom(last ? 0 : 0.5f).BorderColor(RuleLight)
               .PaddingBottom(last ? 0 : 10)
               .Column(p =>
               {
                   p.Item().Text(prj.Title)
                       .FontSize(11).Bold().FontColor(Navy);
                   if (!string.IsNullOrWhiteSpace(prj.Description))
                       p.Item().PaddingTop(1).Text(prj.Description)
                           .FontSize(9).Italic().FontColor(Muted);
                   if (prj.Bullets is { Count: > 0 })
                       p.Item().PaddingTop(4).Column(bullets =>
                       {
                           foreach (var b in prj.Bullets) ComposeBullet(bullets, b);
                       });
               });

            if (!last) col.Item().PaddingBottom(2);
        }
    }

    // ── Activities ───────────────────────────────────────────────
    private void ComposeActivities(ColumnDescriptor col)
    {
        RightHeading(col, "Activities");

        foreach (var act in _data.Activities!)
        {
            col.Item().PaddingBottom(8).Column(a =>
            {
                a.Item().Row(r =>
                {
                    r.AutoItem().Text(act.Title).FontSize(10).Bold().FontColor(Navy);
                    if (!string.IsNullOrWhiteSpace(act.Role))
                        r.AutoItem().Text($"  ·  {act.Role}").FontSize(9).FontColor(Muted);
                });
                if (!string.IsNullOrWhiteSpace(act.Description))
                    a.Item().PaddingTop(2).Text(act.Description)
                        .FontSize(9).FontColor(NavyMid).LineHeight(1.5f);
            });
        }
    }

    // ── Certifications ───────────────────────────────────────────
    private void ComposeCertifications(ColumnDescriptor col)
    {
        RightHeading(col, "Certifications");

        foreach (var cert in _data.Certifications!)
        {
            col.Item().PaddingBottom(4).Row(row =>
            {
                row.AutoItem().PaddingRight(6).Text("·").FontSize(9).FontColor(Navy);
                row.RelativeItem().Text(cert).FontSize(9).FontColor(NavyMid).LineHeight(1.5f);
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════
    private static void ComposeBullet(ColumnDescriptor col, string text)
    {
        col.Item().PaddingBottom(3).Row(row =>
        {
            row.AutoItem().PaddingRight(6).Text("·").FontSize(9).FontColor(Navy);
            row.RelativeItem().Text(text).FontSize(9).FontColor(NavyMid).LineHeight(1.5f);
        });
    }
}
