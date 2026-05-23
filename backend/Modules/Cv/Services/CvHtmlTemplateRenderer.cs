using System.Globalization;
using System.Text.Encodings.Web;
using Fluid;
using NextStep.Modules.Cv.Models;

namespace NextStep.Modules.Cv.Services;

public interface ICvHtmlTemplateRenderer
{
    Task<CvRenderResponse> RenderAsync(string templateSlug, CvData data, CvDesignConfig? designConfig = null);
    CvDesignConfig GetDefaultDesignConfig(string templateSlug);
}

public class CvHtmlTemplateRenderer(IWebHostEnvironment environment) : ICvHtmlTemplateRenderer
{
    private readonly string _templateRoot = Path.Combine(environment.ContentRootPath, "Modules", "Cv", "Templates", "Html");
    private readonly FluidParser _parser = new();

    public async Task<CvRenderResponse> RenderAsync(string templateSlug, CvData data, CvDesignConfig? designConfig = null)
    {
        var normalizedTemplate = NormalizeTemplateSlug(templateSlug);
        var safeData = CvService.SanitizeCvData(data);
        var safeDesign = SanitizeDesignConfig(designConfig, normalizedTemplate);
        var templateHtml = await File.ReadAllTextAsync(Path.Combine(_templateRoot, normalizedTemplate, "template.liquid"));
        var templateCss = await File.ReadAllTextAsync(Path.Combine(_templateRoot, normalizedTemplate, "template.css"));

        if (!_parser.TryParse(templateHtml, out var template, out var error))
        {
            throw new InvalidOperationException($"Unable to parse CV template '{normalizedTemplate}': {error}");
        }

        var model = BuildTemplateModel(normalizedTemplate, safeData, safeDesign);
        var context = new TemplateContext(model);
        var renderedBody = await template.RenderAsync(context);
        var html = $"""
<style>{templateCss}</style>
{renderedBody}
""";

        return new CvRenderResponse
        {
            TemplateSlug = normalizedTemplate,
            DesignConfig = safeDesign,
            Html = html
        };
    }

    public CvDesignConfig GetDefaultDesignConfig(string templateSlug)
    {
        var normalizedTemplate = NormalizeTemplateSlug(templateSlug);
        return normalizedTemplate switch
        {
            "latex" => new CvDesignConfig
            {
                ThemeColor = "#111827",
                FontFamily = "'IBM Plex Sans', 'Segoe UI', Arial, sans-serif",
                FontSize = "13px",
                LineSpacing = "1.38",
                SectionSpacing = "1rem",
                SidebarWidth = "0%"
            },
            _ => new CvDesignConfig
            {
                ThemeColor = "#2d3a8c",
                FontFamily = "Inter, 'Segoe UI', Arial, sans-serif",
                FontSize = "14px",
                LineSpacing = "1.45",
                SectionSpacing = "1.2rem",
                SidebarWidth = "31%"
            }
        };
    }

    private static string NormalizeTemplateSlug(string templateSlug)
    {
        var normalized = (templateSlug ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "latex" or "tech-latex" or "tech_latex" => "latex",
            "modern" => "modern",
            _ => throw new ArgumentException($"Unsupported CV template '{templateSlug}'.")
        };
    }

    private Dictionary<string, object?> BuildTemplateModel(string templateSlug, CvData data, CvDesignConfig designConfig)
    {
        var orderedSections = data.Sections
            .Where(section => section.IsVisible)
            .OrderBy(section => section.Order)
            .ToList();

        return new Dictionary<string, object?>
        {
            ["template_slug"] = templateSlug,
            ["root_style"] = BuildRootStyle(designConfig, templateSlug),
            ["candidate"] = new Dictionary<string, object?>
            {
                ["name"] = data.Candidate.Name,
                ["email"] = data.Candidate.Email,
                ["phone"] = data.Candidate.Phone,
                ["location"] = data.Candidate.Location,
                ["photo_url"] = data.Candidate.PhotoUrl,
                ["linked_in"] = data.Candidate.LinkedIn,
                ["git_hub"] = data.Candidate.GitHub,
                ["portfolio"] = data.Candidate.Portfolio,
                ["initials"] = BuildInitials(data.Candidate.Name)
            },
            ["summary"] = data.Summary,
            ["contact_line"] = string.Join(" • ", new[]
            {
                data.Candidate.Email,
                data.Candidate.Phone,
                data.Candidate.Location
            }.Where(value => !string.IsNullOrWhiteSpace(value))),
            ["profile_links"] = new[]
            {
                ToLink("LinkedIn", data.Candidate.LinkedIn),
                ToLink("GitHub", data.Candidate.GitHub),
                ToLink("Portfolio", data.Candidate.Portfolio)
            }.Where(link => link is not null).ToList(),
            ["main_sections"] = orderedSections.Where(section => section.Placement == CvSectionPlacements.Main).Select(MapSection).ToList(),
            ["sidebar_sections"] = orderedSections.Where(section => section.Placement == CvSectionPlacements.Sidebar).Select(MapSection).ToList(),
            ["has_sidebar"] = orderedSections.Any(section => section.Placement == CvSectionPlacements.Sidebar)
        };
    }

    private static Dictionary<string, object?> MapSection(CvSection section)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = section.Id,
            ["type"] = section.Type,
            ["title"] = section.Title,
            ["text"] = section.Text,
            ["items"] = section.Items.Select(MapItem).ToList()
        };
    }

    private static Dictionary<string, object?> MapItem(CvSectionItem item)
    {
        return new Dictionary<string, object?>
        {
            ["primary_text"] = item.PrimaryText,
            ["secondary_text"] = item.SecondaryText,
            ["date_range"] = BuildDateRange(item.StartDate, item.EndDate),
            ["description"] = item.Description,
            ["location"] = item.Location,
            ["level"] = item.Level,
            ["level_percent"] = Math.Max(20, Math.Min(100, ((item.Level ?? 1) * 20))),
            ["is_matched"] = item.IsMatched,
            ["bullets"] = item.Bullets.Where(bullet => !string.IsNullOrWhiteSpace(bullet)).ToList()
        };
    }

    private static Dictionary<string, object?>? ToLink(string label, string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;
        return new Dictionary<string, object?>
        {
            ["label"] = label,
            ["href"] = href
        };
    }

    private static CvDesignConfig SanitizeDesignConfig(CvDesignConfig? designConfig, string templateSlug)
    {
        var defaults = templateSlug switch
        {
            "latex" => new CvDesignConfig
            {
                ThemeColor = "#111827",
                FontFamily = "'IBM Plex Sans', 'Segoe UI', Arial, sans-serif",
                FontSize = "13px",
                LineSpacing = "1.38",
                SectionSpacing = "1rem",
                SidebarWidth = "0%"
            },
            _ => new CvDesignConfig
            {
                ThemeColor = "#2d3a8c",
                FontFamily = "Inter, 'Segoe UI', Arial, sans-serif",
                FontSize = "14px",
                LineSpacing = "1.45",
                SectionSpacing = "1.2rem",
                SidebarWidth = "31%"
            }
        };

        if (designConfig is null) return defaults;

        return new CvDesignConfig
        {
            ThemeColor = SanitizeColor(designConfig.ThemeColor, defaults.ThemeColor),
            FontFamily = string.IsNullOrWhiteSpace(designConfig.FontFamily) ? defaults.FontFamily : designConfig.FontFamily.Trim(),
            FontSize = SanitizeCssSize(designConfig.FontSize, defaults.FontSize),
            LineSpacing = SanitizeNumber(designConfig.LineSpacing, defaults.LineSpacing),
            SectionSpacing = SanitizeCssSize(designConfig.SectionSpacing, defaults.SectionSpacing),
            SidebarWidth = SanitizeCssSize(designConfig.SidebarWidth, defaults.SidebarWidth)
        };
    }

    private static string BuildRootStyle(CvDesignConfig designConfig, string templateSlug)
    {
        var sidebarWidth = templateSlug == "latex" ? "0%" : designConfig.SidebarWidth;
        return string.Join("; ", new[]
        {
            $"--cv-theme-color: {HtmlEncoder.Default.Encode(designConfig.ThemeColor)}",
            $"--cv-font-family: {HtmlEncoder.Default.Encode(designConfig.FontFamily)}",
            $"--cv-font-size: {HtmlEncoder.Default.Encode(designConfig.FontSize)}",
            $"--cv-line-height: {HtmlEncoder.Default.Encode(designConfig.LineSpacing)}",
            $"--cv-section-spacing: {HtmlEncoder.Default.Encode(designConfig.SectionSpacing)}",
            $"--cv-sidebar-width: {HtmlEncoder.Default.Encode(sidebarWidth)}"
        });
    }

    private static string BuildInitials(string? name)
    {
        var parts = (name ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => part[0].ToString().ToUpperInvariant())
            .ToList();

        return parts.Count == 0 ? "CV" : string.Concat(parts);
    }

    private static string BuildDateRange(string? start, string? end)
    {
        var parts = new[] { FormatDate(start), FormatDate(end) }.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        return parts.Count switch
        {
            0 => string.Empty,
            1 => parts[0],
            _ => $"{parts[0]} - {parts[1]}"
        };
    }

    private static string FormatDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
        {
            return dateOnly.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        }

        return value.Trim();
    }

    private static string SanitizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var trimmed = value.Trim();
        return System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^#(?:[0-9a-fA-F]{3}){1,2}$")
            ? trimmed
            : fallback;
    }

    private static string SanitizeCssSize(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var trimmed = value.Trim();
        return System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+(\.\d+)?(px|rem|em|%)$")
            ? trimmed
            : fallback;
    }

    private static string SanitizeNumber(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString(CultureInfo.InvariantCulture)
            : fallback;
    }
}
