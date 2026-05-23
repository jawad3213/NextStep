using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using NextStep.Modules.Cv.Models;
using System.Collections.Generic;
using System.Linq;
using System;

namespace NextStep.Modules.Cv.Templates;

/// <summary>
/// Circular CV Template: A4, margin 0.
/// Sidebar (32%): Deep background, rounded initials bubble, Contact, Skills, Languages.
/// Content (68%): White background, Candidate Name in bold header, Summary, Experience, Projects, Education, Certifications.
/// </summary>
public class CircularCvDocument : IDocument
{
    private readonly CvData _data;

    private string Font => _data.FontFamily ?? "Inter";
    private string PrimaryColor => _data.ThemeColor ?? "#1A91F0";
    private string SidebarBg => _data.ThemeColor ?? "#1a2b5c";

    private const string White        = "#FFFFFF";
    private const string WhiteMuted   = "#E2E8F0";
    private const string WhiteFaint   = "#94A3B8";
    private const string BodyColor    = "#334155";
    private const string TitleColor   = "#1E293B";
    private const string MutedColor   = "#64748B";
    private const string RuleColor    = "#E2E8F0";
    private const string SkillBarBg  = "#FFFFFF30";

    private const float SidebarW      = 175f;
    private const float AvatarSize    = 70f;
    private const float SidebarPadH   = 16f;
    private const float ContentPadH   = 24f;
    private const float SectionGap    = 16f;

    public CircularCvDocument(CvData data) => _data = data;
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(x => x.FontFamily(Font).FontSize(9f).FontColor(BodyColor));

            page.Content().Row(row =>
            {
                // Left Sidebar
                row.ConstantItem(SidebarW)
                    .Background(SidebarBg)
                    .PaddingHorizontal(SidebarPadH)
                    .PaddingTop(28).PaddingBottom(36)
                    .Column(sidebar =>
                    {
                        // Initials Framed Badge
                        sidebar.Item().AlignCenter().Width(AvatarSize).Height(AvatarSize)
                            .Background(SidebarBg).Border(1.5f).BorderColor(Colors.White).AlignCenter().AlignMiddle()
                            .Text(GetInitials(_data.Candidate.Name))
                            .FontFamily(Font).FontSize(20).Bold().FontColor(Colors.White);

                        SidebarContact(sidebar);
                        SidebarSkills(sidebar);
                        SidebarLanguages(sidebar);
                    });

                // Right Content Area
                row.RelativeItem()
                    .Background(White)
                    .PaddingHorizontal(ContentPadH)
                    .PaddingTop(28).PaddingBottom(36)
                    .Column(content =>
                    {
                        // Header with Name
                        content.Item().PaddingBottom(12).Column(h =>
                        {
                            h.Item().Text(_data.Candidate.Name.ToUpperInvariant())
                             .FontSize(24).Bold().FontColor(PrimaryColor).LetterSpacing(0.08f);

                            if (_data.Skills is { Count: > 0 })
                            {
                                var mainSkill = _data.Skills.First().Name;
                                h.Item().PaddingTop(2).Text(mainSkill.ToUpperInvariant())
                                 .FontSize(10f).Bold().FontColor(MutedColor).LetterSpacing(0.12f);
                            }
                        });

                        ContentSummary(content);
                        ContentExperience(content);
                        ContentProjects(content);
                        ContentEducation(content);
                        ContentCertifications(content);
                        ContentActivities(content);
                    });
            });
        });
    }

    private static void SidebarHeading(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(SectionGap).Column(h =>
        {
            h.Item().Text(title.ToUpperInvariant()).FontSize(8f).Bold().FontColor(White).LetterSpacing(0.1f);
            h.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor("#FFFFFF25");
        });
        col.Item().PaddingBottom(8);
    }

    private void SidebarContact(ColumnDescriptor col)
    {
        SidebarHeading(col, "Contact");
        SidebarContactRow(col, "✉", _data.Candidate.Email);
        SidebarContactRow(col, "☎", _data.Candidate.Phone);
        SidebarContactRow(col, "⌂", _data.Candidate.Location);
        SidebarContactRow(col, "🔗", _data.Candidate.LinkedIn);
    }

    private static void SidebarContactRow(ColumnDescriptor col, string icon, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().PaddingBottom(6).Row(row =>
        {
            row.AutoItem().PaddingRight(6).Text(icon).FontSize(9f).FontColor(WhiteMuted);
            row.RelativeItem().Text(value).FontSize(8f).FontColor(WhiteMuted).LineHeight(1.3f);
        });
    }

    private void SidebarSkills(ColumnDescriptor col)
    {
        if (_data.Skills is not { Count: > 0 }) return;
        SidebarHeading(col, "Skills");
        foreach (var skill in _data.Skills.Take(12)) // Show up to 12 skills
        {
            col.Item().PaddingBottom(6).Column(s =>
            {
                s.Item().Row(r =>
                {
                    r.RelativeItem().Text(skill.Name).FontSize(8f).FontColor(White);
                    if (skill.IsMatched)
                        r.AutoItem().Text("★").FontSize(7f).FontColor(White);
                });
                s.Item().PaddingTop(2).Height(2f).Background(SkillBarBg).Row(bar =>
                {
                    bar.RelativeItem(skill.Level).Height(2f).Background(White);
                    if (skill.Level < 10) bar.RelativeItem(10 - skill.Level).Height(2f).Background(Colors.Transparent);
                });
            });
        }
    }

    private void SidebarLanguages(ColumnDescriptor col)
    {
        if (_data.Languages is not { Count: > 0 }) return;
        SidebarHeading(col, "Languages");
        col.Item().Text(string.Join("  ·  ", _data.Languages)).FontSize(8f).FontColor(WhiteMuted).LineHeight(1.4f);
    }

    private void ContentHeading(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(SectionGap).Column(h =>
        {
            h.Item().Text(title.ToUpperInvariant()).FontSize(9f).Bold().FontColor(PrimaryColor).LetterSpacing(0.08f);
            h.Item().PaddingTop(3).LineHorizontal(0.8f).LineColor(RuleColor);
        });
        col.Item().PaddingBottom(8);
    }

    private void ContentSummary(ColumnDescriptor col)
    {
        if (string.IsNullOrWhiteSpace(_data.Summary)) return;
        ContentHeading(col, "Profil");
        col.Item().Text(_data.Summary).FontSize(9f).FontColor(BodyColor).LineHeight(1.6f);
    }

    private void ContentExperience(ColumnDescriptor col)
    {
        if (_data.Experience is not { Count: > 0 }) return;
        ContentHeading(col, "Expériences Professionnelles");
        for (int i = 0; i < _data.Experience.Count; i++)
        {
            var exp = _data.Experience[i];
            bool last = i == _data.Experience.Count - 1;
            col.Item().PaddingBottom(last ? 0 : 4).Column(item =>
            {
                item.Item().Row(r =>
                {
                    r.RelativeItem().Text(exp.Role).FontSize(10.5f).Bold().FontColor(TitleColor);
                    var dateStr = exp.End is not null ? $"{exp.Start} – {exp.End}" : $"{exp.Start} – Present";
                    r.AutoItem().Text(dateStr).FontSize(8.5f).Italic().FontColor(MutedColor);
                });
                item.Item().PaddingTop(1).PaddingBottom(4).Text(exp.Company).FontSize(8.5f).Italic().FontColor(MutedColor);
                if (exp.Bullets is { Count: > 0 })
                    item.Item().Column(bullets => { foreach (var b in exp.Bullets) BulletRow(bullets, b); });
            });
            if (!last) col.Item().PaddingVertical(6).LineHorizontal(0.4f).LineColor(RuleColor);
        }
    }

    private void ContentProjects(ColumnDescriptor col)
    {
        if (_data.Projects is not { Count: > 0 }) return;
        ContentHeading(col, "Projets");
        foreach (var prj in _data.Projects)
        {
            col.Item().PaddingBottom(8).Column(item =>
            {
                item.Item().Text(prj.Title).FontSize(10f).Bold().FontColor(TitleColor);
                if (!string.IsNullOrWhiteSpace(prj.Description))
                    item.Item().PaddingTop(1).PaddingBottom(3).Text(prj.Description).FontSize(8.5f).Italic().FontColor(MutedColor);
                if (prj.Bullets is { Count: > 0 })
                    item.Item().Column(bullets => { foreach (var b in prj.Bullets) BulletRow(bullets, b); });
            });
        }
    }

    private void ContentEducation(ColumnDescriptor col)
    {
        if (_data.Education is not { Count: > 0 }) return;
        ContentHeading(col, "Formations");
        foreach (var edu in _data.Education)
        {
            col.Item().PaddingBottom(6).Row(r =>
            {
                r.RelativeItem().Column(e =>
                {
                    e.Item().Text(edu.Degree).FontSize(9f).Bold().FontColor(TitleColor);
                    e.Item().Text(edu.Institution).FontSize(8.5f).Italic().FontColor(MutedColor);
                });
                r.AutoItem().Text(edu.Year).FontSize(8f).FontColor(MutedColor);
            });
        }
    }

    private void ContentCertifications(ColumnDescriptor col)
    {
        if (_data.Certifications is not { Count: > 0 }) return;
        ContentHeading(col, "Certifications");
        foreach (var cert in _data.Certifications)
            col.Item().PaddingBottom(3).Row(row =>
            {
                row.AutoItem().PaddingRight(5).Text("•").FontSize(9f).FontColor(PrimaryColor);
                row.RelativeItem().Text(cert).FontSize(8.5f).FontColor(BodyColor);
            });
    }

    private void ContentActivities(ColumnDescriptor col)
    {
        if (_data.Activities is not { Count: > 0 }) return;
        ContentHeading(col, "Activités / Autres");
        foreach (var act in _data.Activities)
            col.Item().PaddingBottom(6).Column(item =>
            {
                item.Item().Row(r =>
                {
                    r.AutoItem().Text(act.Title).FontSize(9.5f).Bold().FontColor(TitleColor);
                    if (!string.IsNullOrWhiteSpace(act.Role))
                        r.AutoItem().Text($"  —  {act.Role}").FontSize(8.5f).Italic().FontColor(MutedColor);
                });
                if (!string.IsNullOrWhiteSpace(act.Description))
                    item.Item().PaddingTop(2).Text(act.Description).FontSize(8.5f).LineHeight(1.4f);
            });
    }

    private static void BulletRow(ColumnDescriptor col, string text)
    {
        col.Item().PaddingBottom(2).Row(row =>
        {
            row.AutoItem().PaddingRight(5).Text("•").FontSize(8.5f).FontColor(MutedColor);
            row.RelativeItem().Text(text).FontSize(8.5f).FontColor(BodyColor).LineHeight(1.45f);
        });
    }

    private string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "NS";
        var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant() 
            : parts[0][0].ToString().ToUpperInvariant();
    }
}
