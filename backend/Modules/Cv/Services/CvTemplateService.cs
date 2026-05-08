using Microsoft.EntityFrameworkCore;
using NextStep.data;
using NextStep.Modules.Cv.Models;

namespace NextStep.Modules.Cv.Services;

public interface ICvTemplateService
{
    /// <summary>
    /// Returns all active templates, optionally filtered by the given criteria.
    /// </summary>
    Task<List<CvTemplateDto>> GetTemplatesAsync(CvTemplateFilterQuery? filter = null);

    /// <summary>
    /// Returns the available filter options (populated enum values).
    /// </summary>
    CvTemplateFilterOptions GetFilterOptions();
}

/// <summary>
/// Describes all available filter options for the frontend dropdowns.
/// </summary>
public class CvTemplateFilterOptions
{
    public List<FilterOption> Industries { get; set; } = new();
    public List<FilterOption> ExperienceLevels { get; set; } = new();
    public List<FilterOption> Styles { get; set; } = new();
    public List<FilterOption> Layouts { get; set; } = new();
}

public class FilterOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class CvTemplateService : ICvTemplateService
{
    private readonly AppDbContext _db;

    public CvTemplateService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CvTemplateDto>> GetTemplatesAsync(CvTemplateFilterQuery? filter = null)
    {
        IQueryable<CvTemplate> query = _db.CvTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder);

        // Apply filters
        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Industry) &&
                Enum.TryParse<CvIndustry>(filter.Industry, true, out var industry))
            {
                query = query.Where(t => t.Industries.Contains(industry));
            }

            if (!string.IsNullOrWhiteSpace(filter.ExperienceLevel) &&
                Enum.TryParse<CvExperienceLevel>(filter.ExperienceLevel, true, out var level))
            {
                query = query.Where(t => t.ExperienceLevels.Contains(level));
            }

            if (!string.IsNullOrWhiteSpace(filter.Style) &&
                Enum.TryParse<CvTemplateStyle>(filter.Style, true, out var style))
            {
                query = query.Where(t => t.Style == style);
            }

            if (!string.IsNullOrWhiteSpace(filter.Layout) &&
                Enum.TryParse<CvTemplateLayout>(filter.Layout, true, out var layout))
            {
                query = query.Where(t => t.Layout.HasFlag(layout));
            }

            if (!string.IsNullOrWhiteSpace(filter.Color))
            {
                query = query.Where(t => t.BackgroundColor == filter.Color);
            }

            if (!string.IsNullOrWhiteSpace(filter.Tag))
            {
                var tag = filter.Tag.ToLowerInvariant();
                query = query.Where(t => t.Tags.Contains(tag));
            }
        }

        var templates = await query.ToListAsync();

        return templates.Select(MapToDto).ToList();
    }

    public CvTemplateFilterOptions GetFilterOptions()
    {
        return new CvTemplateFilterOptions
        {
            Industries = new List<FilterOption>
            {
                new() { Value = "AdministrativeAndOffice",    Label = "Administrative & Office" },
                new() { Value = "BusinessAndManagement",      Label = "Business & Management" },
                new() { Value = "CreativeAndDesign",          Label = "Creative & Design" },
                new() { Value = "CustomerServiceAndRetail",   Label = "Customer Service & Retail" },
                new() { Value = "EducationAndAcademic",       Label = "Education & Academic" },
                new() { Value = "FinanceAndAccounting",       Label = "Finance & Accounting" },
                new() { Value = "FoodServiceAndHospitality",  Label = "Food Service & Hospitality" },
                new() { Value = "HealthcareAndMedical",       Label = "Healthcare & Medical" },
                new() { Value = "ITAndEngineering",           Label = "IT & Engineering" },
                new() { Value = "MarketingAndSales",          Label = "Marketing & Sales" },
                new() { Value = "Other",                      Label = "Other" },
            },
            ExperienceLevels = new List<FilterOption>
            {
                new() { Value = "StudentEntryLevel", Label = "Student / Entry Level" },
                new() { Value = "MidLevel",          Label = "Mid Level" },
                new() { Value = "SeniorExecutive",   Label = "Senior / Executive" },
            },
            Styles = new List<FilterOption>
            {
                new() { Value = "Corporate",    Label = "Corporate" },
                new() { Value = "Creative",     Label = "Creative" },
                new() { Value = "Elegant",      Label = "Elegant" },
                new() { Value = "Modern",       Label = "Modern" },
                new() { Value = "Professional", Label = "Professional" },
                new() { Value = "Simple",       Label = "Simple" },
                new() { Value = "Traditional",  Label = "Traditional" },
            },
            Layouts = new List<FilterOption>
            {
                new() { Value = "OneColumn",    Label = "One Column" },
                new() { Value = "TwoColumn",    Label = "Two Column" },
                new() { Value = "WithPhoto",    Label = "With Photo" },
                new() { Value = "WithoutPhoto", Label = "Without Photo" },
                new() { Value = "OnePage",      Label = "One Page" },
                new() { Value = "TwoPage",      Label = "Two Page" },
            },
        };
    }

    // ─── Mapping ───────────────────────────────────────────────────

    private static CvTemplateDto MapToDto(CvTemplate t) => new()
    {
        Id              = t.Id,
        Slug            = t.Slug,
        Name            = t.Name,
        Description     = t.Description,
        ThumbnailUrl    = t.ThumbnailUrl,
        Industries      = t.Industries.Select(IndustryLabel).ToList(),
        ExperienceLevels = t.ExperienceLevels.Select(LevelLabel).ToList(),
        Style           = t.Style.ToString(),
        LayoutFlags     = ParseLayoutFlags(t.Layout),
        BackgroundColor = t.BackgroundColor,
        Tags            = t.Tags,
    };

    private static string IndustryLabel(CvIndustry i) => i switch
    {
        CvIndustry.AdministrativeAndOffice   => "Administrative & Office",
        CvIndustry.BusinessAndManagement     => "Business & Management",
        CvIndustry.CreativeAndDesign         => "Creative & Design",
        CvIndustry.CustomerServiceAndRetail  => "Customer Service & Retail",
        CvIndustry.EducationAndAcademic      => "Education & Academic",
        CvIndustry.FinanceAndAccounting      => "Finance & Accounting",
        CvIndustry.FoodServiceAndHospitality => "Food Service & Hospitality",
        CvIndustry.HealthcareAndMedical      => "Healthcare & Medical",
        CvIndustry.ITAndEngineering          => "IT & Engineering",
        CvIndustry.MarketingAndSales         => "Marketing & Sales",
        _                                    => "Other",
    };

    private static string LevelLabel(CvExperienceLevel l) => l switch
    {
        CvExperienceLevel.StudentEntryLevel => "Student / Entry Level",
        CvExperienceLevel.MidLevel          => "Mid Level",
        CvExperienceLevel.SeniorExecutive   => "Senior / Executive",
        _                                   => l.ToString(),
    };

    private static List<string> ParseLayoutFlags(CvTemplateLayout layout)
    {
        var flags = new List<string>();
        if (layout.HasFlag(CvTemplateLayout.OneColumn))    flags.Add("One Column");
        if (layout.HasFlag(CvTemplateLayout.TwoColumn))    flags.Add("Two Column");
        if (layout.HasFlag(CvTemplateLayout.WithPhoto))    flags.Add("With Photo");
        if (layout.HasFlag(CvTemplateLayout.WithoutPhoto)) flags.Add("Without Photo");
        if (layout.HasFlag(CvTemplateLayout.OnePage))      flags.Add("One Page");
        if (layout.HasFlag(CvTemplateLayout.TwoPage))      flags.Add("Two Page");
        return flags;
    }
}
