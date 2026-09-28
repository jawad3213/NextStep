using NextStep.Modules.Cv.Models;
using NextStep.Modules.Cv.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NextStep.Modules.Cv.Templates;

/// <summary>
/// Modern CV template: A4, full-width dark blue header, two-column body layout.
/// Left column (35%): Skills, Education, Languages.
/// Right column (65%): Summary, Experience, Projects, Activities, Certifications.
/// </summary>
public class ModernCvDocument : IDocument, ICvTemplateStyle
{
    private readonly CvData _data;

    public ModernCvDocument(CvData data)
    {
        _data = CvService.SanitizeCvData(data);
    }

    public string FontName => "Inter";
    public string PrimaryColor => _data.ThemeColor ?? "#2d3a8c";

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

            page.Header().ShowOnce().Background(PrimaryColor).Padding(25).Row(headerRow =>
            {
                headerRow.AutoItem().PaddingRight(14).Element(ComposeHeaderAvatar);

                headerRow.RelativeItem().Column(column =>
                {
                    column.Item().Text(_data.Candidate.Name)
                        .FontFamily(FontName).FontSize(26).Bold().FontColor("#ffffff");

                    column.Item().PaddingTop(4).Text(ComposeHeaderMeta())
                        .FontFamily(FontName).FontSize(10).FontColor(HeaderLightText);
                });
            });

            page.Content().Row(row =>
            {
                row.RelativeItem(35).Background(SidebarBg).Padding(20).Column(column =>
                {
                    foreach (var section in CvSectionMapper.VisibleSections(_data, CvSectionPlacements.Sidebar))
                        column.Item().Component(new CvSectionComponent(section, this));
                });

                row.RelativeItem(65).Background("#ffffff").Padding(20).Column(column =>
                {
                    foreach (var section in CvSectionMapper.VisibleSections(_data, CvSectionPlacements.Main))
                        column.Item().Component(new CvSectionComponent(section, this));
                });
            });
        });
    }

    public void DrawSectionTitle(ColumnDescriptor column, CvSection section)
    {
        var color = section.Placement == CvSectionPlacements.Sidebar ? TitleColor : TitleColor;
        column.Item().PaddingTop(14).PaddingBottom(6).Column(titleCol =>
        {
            titleCol.Item().Text(section.Title).FontFamily(FontName).FontSize(12).Bold().FontColor(color);
            titleCol.Item().PaddingTop(2).LineHorizontal(1).LineColor(PrimaryColor);
        });
    }

    public void DrawSectionText(ColumnDescriptor column, CvSection section)
    {
        column.Item().PaddingBottom(10).Text(section.Text ?? string.Empty)
            .FontFamily(FontName).FontSize(10).FontColor(BodyColor).LineHeight(1.6f);
    }

    public void DrawSectionItem(ColumnDescriptor column, CvSection section, CvSectionItem item)
    {
        if (section.Placement == CvSectionPlacements.Sidebar)
        {
            DrawSidebarItem(column, section, item);
            return;
        }

        DrawMainItem(column, section, item);
    }

    public void DrawSectionDivider(ColumnDescriptor column, CvSection section)
    {
    }

    private string ComposeHeaderMeta()
    {
        var values = new List<string> { _data.Candidate.Email };
        if (!string.IsNullOrWhiteSpace(_data.Candidate.Location))
            values.Add(_data.Candidate.Location);
        if (!string.IsNullOrWhiteSpace(_data.Candidate.Phone))
            values.Add(_data.Candidate.Phone);
        if (!string.IsNullOrWhiteSpace(_data.Candidate.LinkedIn))
            values.Add(_data.Candidate.LinkedIn);
        if (!string.IsNullOrWhiteSpace(_data.Candidate.GitHub))
            values.Add(_data.Candidate.GitHub);
        return string.Join(" | ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private void DrawSidebarItem(ColumnDescriptor column, CvSection section, CvSectionItem item)
    {
        switch (section.Type)
        {
            case CvSectionTypes.Skills:
                var level = Math.Clamp(item.Level ?? 1, 1, 5);
                var pct = level * 20;
                column.Item().PaddingBottom(8).Column(skill =>
                {
                    skill.Item().Row(row =>
                    {
                        row.RelativeItem().Text(item.PrimaryText).FontFamily(FontName).FontSize(9.3f).FontColor(BodyColor);
                        row.AutoItem().Text($"{pct}%").FontFamily(FontName).FontSize(8.5f).Bold()
                            .FontColor(item.IsMatched ? PrimaryColor : GrayText);
                    });

                    skill.Item().PaddingTop(2).Height(4).Background("#d9deee").Row(bar =>
                    {
                        bar.RelativeItem(pct).Background(item.IsMatched ? PrimaryColor : "#8d97bf");
                        bar.RelativeItem(100 - pct);
                    });
                });
                break;

            case CvSectionTypes.Education:
                column.Item().PaddingBottom(10).Column(e =>
                {
                    e.Item().Row(row =>
                    {
                        row.RelativeItem().Text(item.PrimaryText).FontFamily(FontName).FontSize(10).Bold().FontColor(TitleColor);
                        var educationPeriod = ComposeEducationPeriod(item);
                        if (!string.IsNullOrWhiteSpace(educationPeriod))
                            row.AutoItem().Text(educationPeriod).FontFamily(FontName).FontSize(8.5f).FontColor(GrayText);
                    });
                    e.Item().PaddingTop(1).Text(item.SecondaryText).FontFamily(FontName).FontSize(9).Italic().FontColor(GrayText);
                    if (!string.IsNullOrWhiteSpace(item.Description))
                        e.Item().PaddingTop(1).Text(item.Description).FontFamily(FontName).FontSize(9).FontColor(GrayText);
                });
                break;

            default:
                column.Item().PaddingBottom(4).Row(row =>
                {
                    row.AutoItem().PaddingRight(5).Text("-").FontFamily(FontName).FontSize(9).FontColor(BodyColor);
                    row.RelativeItem().Text(item.PrimaryText).FontFamily(FontName).FontSize(9).FontColor(BodyColor);
                });
                break;
        }
    }

    private void DrawMainItem(ColumnDescriptor column, CvSection section, CvSectionItem item)
    {
        switch (section.Type)
        {
            case CvSectionTypes.Experience:
                column.Item().PaddingBottom(12).Column(itemColumn =>
                {
                    itemColumn.Item().Row(r =>
                    {
                        r.RelativeItem().Text(item.PrimaryText).FontFamily(FontName).FontSize(11).Bold().FontColor(TitleColor);
                        var dateStr = ComposeDateRange(item);
                        if (!string.IsNullOrWhiteSpace(dateStr))
                            r.AutoItem().Text(dateStr).FontFamily(FontName).FontSize(9).FontColor(GrayText);
                    });
                    itemColumn.Item().PaddingTop(1).Text(item.SecondaryText).FontFamily(FontName).FontSize(10).Italic().FontColor(GrayText);
                    foreach (var bullet in item.Bullets)
                        BulletRow(itemColumn, bullet);
                });
                break;

            case CvSectionTypes.Projects:
                column.Item().PaddingBottom(10).Column(itemColumn =>
                {
                    itemColumn.Item().Row(r =>
                    {
                        r.RelativeItem().Text(item.PrimaryText).FontFamily(FontName).FontSize(11).Bold().FontColor(TitleColor);
                        var dateRealisation = FormatDateToken(item.StartDate);
                        if (!string.IsNullOrWhiteSpace(dateRealisation))
                            r.AutoItem().Text(dateRealisation).FontFamily(FontName).FontSize(9).FontColor(GrayText);
                    });
                    if (!string.IsNullOrWhiteSpace(item.SecondaryText))
                        itemColumn.Item().PaddingTop(1).Text(item.SecondaryText).FontFamily(FontName).FontSize(9).FontColor(PrimaryColor);
                    if (!string.IsNullOrWhiteSpace(item.Description))
                        itemColumn.Item().PaddingTop(1).Text(item.Description).FontFamily(FontName).FontSize(9).Italic().FontColor(GrayText);
                    foreach (var bullet in item.Bullets)
                        BulletRow(itemColumn, bullet);
                });
                break;

            case CvSectionTypes.Activities:
                column.Item().PaddingBottom(8).Column(itemColumn =>
                {
                    itemColumn.Item().Row(r =>
                    {
                        r.AutoItem().Text(item.PrimaryText).FontFamily(FontName).FontSize(11).Bold().FontColor(TitleColor);
                        if (!string.IsNullOrWhiteSpace(item.SecondaryText))
                            r.AutoItem().Text($"  -  {item.SecondaryText}").FontFamily(FontName).FontSize(10).FontColor(GrayText);
                    });
                    if (!string.IsNullOrWhiteSpace(item.Description))
                        itemColumn.Item().PaddingTop(2).Text(item.Description).FontFamily(FontName).FontSize(9).FontColor(BodyColor).LineHeight(1.5f);
                });
                break;

            default:
                column.Item().PaddingBottom(4).Row(row =>
                {
                    row.AutoItem().PaddingRight(5).Text("-").FontFamily(FontName).FontSize(9).FontColor(PrimaryColor);
                    row.RelativeItem().Text(item.PrimaryText).FontFamily(FontName).FontSize(9).FontColor(BodyColor);
                });
                break;
        }
    }

    private void BulletRow(ColumnDescriptor column, string value)
    {
        column.Item().PaddingTop(4).PaddingBottom(3).Row(row =>
        {
            row.AutoItem().PaddingRight(5).Text("-").FontFamily(FontName).FontSize(9).FontColor(BodyColor);
            row.RelativeItem().Text(value).FontFamily(FontName).FontSize(9).FontColor(BodyColor).LineHeight(1.5f);
        });
    }

    private void ComposeHeaderAvatar(IContainer container)
    {
        var photoBytes = TryGetPhotoBytes(_data.Candidate.PhotoUrl);
        container.Width(64).Height(64).Background("#ffffff22").AlignMiddle().AlignCenter().Element(avatar =>
        {
            if (photoBytes is not null)
            {
                avatar.Image(photoBytes).FitArea();
                return;
            }

            var initials = GetInitials(_data.Candidate.Name);
            avatar.Text(initials).FontFamily(FontName).FontSize(20).Bold().FontColor("#ffffff");
        });
    }

    private static byte[]? TryGetPhotoBytes(string? photoUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
            return null;

        var trimmed = photoUrl.Trim();
        if (!trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return null;

        var commaIndex = trimmed.IndexOf(',');
        if (commaIndex <= 0 || commaIndex >= trimmed.Length - 1)
            return null;

        var base64 = trimmed[(commaIndex + 1)..];
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch
        {
            return null;
        }
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "CV";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();

        return string.Concat(parts[0][0], parts[^1][0]).ToUpperInvariant();
    }

    private static string ComposeDateRange(CvSectionItem item)
    {
        if (string.IsNullOrWhiteSpace(item.StartDate) && string.IsNullOrWhiteSpace(item.EndDate))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(item.EndDate))
            return $"{item.StartDate} - Present";

        if (string.IsNullOrWhiteSpace(item.StartDate))
            return item.EndDate ?? string.Empty;

        return $"{item.StartDate} - {item.EndDate}";
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

    private static string FormatDateToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        if (DateTime.TryParse(raw, out var parsed))
            return parsed.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

        return raw.Trim();
    }

}
