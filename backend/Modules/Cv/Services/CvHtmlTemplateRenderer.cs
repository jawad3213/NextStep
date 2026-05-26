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
            ["skill_summary_rows"] = BuildSkillSummaryRows(data),
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

    private static List<Dictionary<string, object?>> BuildSkillSummaryRows(CvData data)
    {
        var sourceSkills = (data.TechnicalSkills.Count > 0
                ? data.TechnicalSkills
                : data.Skills.Where(s => !IsSoftSkill(s)))
            .Where(skill => !string.IsNullOrWhiteSpace(skill.Name))
            .GroupBy(skill => skill.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (sourceSkills.Count == 0)
        {
            sourceSkills = data.Sections
                .Where(section => IsTechnicalSkillSection(section))
                .SelectMany(section => section.Items)
                .Where(item => !string.IsNullOrWhiteSpace(item.PrimaryText))
                .GroupBy(item => item.PrimaryText.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => new CvSkill
                {
                    Name = group.First().PrimaryText.Trim(),
                    Level = group.First().Level ?? 3,
                    IsMatched = group.First().IsMatched,
                    Category = group
                        .Select(item => item.SecondaryText?.Trim())
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                })
                .ToList();
        }

        var buckets = new[]
        {
            ("Languages", new List<string>()),
            ("Backend", new List<string>()),
            ("Frontend", new List<string>()),
            ("Databases", new List<string>()),
            ("DevOps & Cloud", new List<string>()),
            ("QA & Testing", new List<string>()),
            ("AI & Tools", new List<string>()),
            ("Methodologies", new List<string>()),
            ("Other", new List<string>()),
        };

        foreach (var skill in sourceSkills)
        {
            var label = ResolveSkillBucket(skill);
            var bucket = buckets.First(tuple => tuple.Item1 == label).Item2;
            bucket.Add(skill.Name.Trim());
        }

        return buckets
            .Where(tuple => tuple.Item2.Count > 0)
            .Select(tuple => new Dictionary<string, object?>
            {
                ["label"] = tuple.Item1,
                ["value"] = string.Join(", ", tuple.Item2.Distinct(StringComparer.OrdinalIgnoreCase))
            })
            .ToList();
    }

    private static string ResolveSkillBucket(CvSkill skill)
    {
        var name = (skill.Name ?? string.Empty).Trim();
        var normalized = name.ToLowerInvariant();
        var category = (skill.Category ?? string.Empty).Trim().ToLowerInvariant();

        if (category.Contains("frontend")) return "Frontend";
        if (category.Contains("backend")) return "Backend";
        if (category.Contains("database")) return "Databases";
        if (category.Contains("devops") || category.Contains("cloud")) return "DevOps & Cloud";
        if (category.Contains("test") || category.Contains("qa")) return "QA & Testing";
        if (category.Contains("ai") || category.Contains("tool")) return "AI & Tools";
        if (category.Contains("method")) return "Methodologies";
        if (category.Contains("language")) return "Languages";

        if (new[] { "java", "javascript", "typescript", "python", "c#", "c", "c++", "php", "go", "ruby" }.Contains(normalized))
            return "Languages";
        if (new[] { "node.js", "nodejs", "express.js", "expressjs", "laravel", "fastapi", "spring boot", "nestjs", "django", "flask" }.Contains(normalized))
            return "Backend";
        if (new[] { "angular", "react", "react.js", "reactjs", "vue.js", "vuejs", "tailwind css", "html5", "css", "gsap" }.Contains(normalized))
            return "Frontend";
        if (new[] { "postgresql", "mysql", "mongodb", "oracle db", "sql", "sqlite", "redis" }.Contains(normalized))
            return "Databases";
        if (new[] { "git", "github actions", "docker", "linux", "terraform", "aws", "aws ec2", "aws s3", "azure", "kubernetes", "jenkins" }.Contains(normalized))
            return "DevOps & Cloud";
        if (new[] { "playwright", "cypress", "selenium", "api testing", "e2e automation frameworks", "qa automation", "postman", "restassured", "supertest" }.Contains(normalized))
            return "QA & Testing";
        if (new[] { "rag", "openai api", "n8n", "gemini api", "jira", "figma", "ai agents", "swagger" }.Contains(normalized))
            return "AI & Tools";
        if (new[] { "agile", "scrum", "scrum methodologies", "agile/scrum" }.Contains(normalized))
            return "Methodologies";

        return "Other";
    }

    private static bool IsTechnicalSkillSection(CvSection section)
    {
        var id = (section.Id ?? string.Empty).Trim();
        var type = (section.Type ?? string.Empty).Trim();

        return string.Equals(type, CvSectionTypes.Skills, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "skill", StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, CvSectionTypes.Skills, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, "skill", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSoftSkill(CvSkill? skill)
    {
        var category = (skill?.Category ?? string.Empty).Trim().ToLowerInvariant();
        var typeCompetence = (skill?.TypeCompetence ?? string.Empty).Trim().ToLowerInvariant();

        return category is "soft" or "soft skill" or "soft skills"
            || typeCompetence is "soft" or "soft skill" or "soft skills" or "comportemental" or "behavioral" or "behavioural";
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
