using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NextStep.Modules.Cv.Models;

namespace NextStep.Modules.Cv.Services;

public static class CvSectionMapper
{
    public static List<CvSection> BuildSectionsFromLegacy(CvData data)
    {
        var sections = new List<CvSection>();
        var order = 0;

        var technicalSkills = data.TechnicalSkills.Count > 0
            ? data.TechnicalSkills
            : data.Skills.Where(s => s.Category != "Soft Skills" && s.TypeCompetence != "Comportemental").ToList();

        var softSkills = data.SoftSkills.Count > 0
            ? data.SoftSkills
            : data.Skills.Where(s => s.Category == "Soft Skills" || s.TypeCompetence == "Comportemental").ToList();

        sections.Add(new CvSection
        {
            Id = CvSectionTypes.Summary,
            Type = CvSectionTypes.Summary,
            Title = "Summary",
            Placement = CvSectionPlacements.Main,
            IsVisible = !string.IsNullOrWhiteSpace(data.Summary),
            Order = order++,
            Text = data.Summary
        });

        sections.Add(new CvSection
        {
            Id = CvSectionTypes.Experience,
            Type = CvSectionTypes.Experience,
            Title = "Experience",
            Placement = CvSectionPlacements.Main,
            IsVisible = data.Experience.Count > 0,
            Order = order++,
            Items = data.Experience.Select(exp => new CvSectionItem
            {
                PrimaryText = exp.Role,
                SecondaryText = exp.Company,
                StartDate = exp.Start,
                EndDate = exp.End,
                Bullets = exp.Bullets.ToList()
            }).ToList()
        });

        sections.Add(new CvSection
        {
            Id = CvSectionTypes.Projects,
            Type = CvSectionTypes.Projects,
            Title = "Projects",
            Placement = CvSectionPlacements.Main,
            IsVisible = data.Projects.Count > 0,
            Order = order++,
            Items = data.Projects.Select(project => new CvSectionItem
            {
                PrimaryText = project.Title,
                SecondaryText = string.Join(", ", project.Technologies ?? new List<string>()),
                StartDate = project.DateRealisation,
                Description = project.Description,
                Bullets = project.Bullets.ToList()
            }).ToList()
        });

        sections.Add(new CvSection
        {
            Id = CvSectionTypes.Certifications,
            Type = CvSectionTypes.Certifications,
            Title = "Certifications",
            Placement = CvSectionPlacements.Main,
            IsVisible = data.Certifications.Count > 0,
            Order = order++,
            Items = data.Certifications.Select(certification => new CvSectionItem
            {
                PrimaryText = certification
            }).ToList()
        });

        sections.Add(new CvSection
        {
            Id = CvSectionTypes.Activities,
            Type = CvSectionTypes.Activities,
            Title = "Activities",
            Placement = CvSectionPlacements.Main,
            IsVisible = data.Activities.Count > 0,
            Order = order++,
            Items = data.Activities.Select(activity => new CvSectionItem
            {
                PrimaryText = activity.Title,
                SecondaryText = activity.Role ?? string.Empty,
                Description = activity.Description,
                StartDate = activity.StartDate,
                EndDate = activity.EndDate
            }).ToList()
        });

        sections.Add(new CvSection
        {
            Id = CvSectionTypes.Skills,
            Type = CvSectionTypes.Skills,
            Title = "Skills",
            Placement = CvSectionPlacements.Sidebar,
            IsVisible = technicalSkills.Count > 0,
            Order = order++,
            Items = technicalSkills.Select(skill => new CvSectionItem
            {
                PrimaryText = skill.Name,
                Level = skill.Level,
                IsMatched = skill.IsMatched
            }).ToList()
        });

        sections.Add(new CvSection
        {
            Id = CvSectionTypes.SoftSkills,
            Type = CvSectionTypes.SoftSkills,
            Title = "Soft Skills",
            Placement = CvSectionPlacements.Sidebar,
            IsVisible = softSkills.Count > 0,
            Order = order++,
            Items = softSkills.Select(skill => new CvSectionItem
            {
                PrimaryText = skill.Name,
                Level = skill.Level,
                IsMatched = skill.IsMatched
            }).ToList()
        });

        sections.Add(new CvSection
        {
            Id = CvSectionTypes.Education,
            Type = CvSectionTypes.Education,
            Title = "Education",
            Placement = CvSectionPlacements.Sidebar,
            IsVisible = data.Education.Count > 0,
            Order = order++,
            Items = data.Education.Select(education => new CvSectionItem
            {
                PrimaryText = education.Degree,
                SecondaryText = education.Institution,
                StartDate = education.StartYear,
                EndDate = education.EndYear,
                Description = string.IsNullOrWhiteSpace(education.Year)
                    ? null
                    : education.Year
            }).ToList()
        });

        sections.Add(new CvSection
        {
            Id = CvSectionTypes.Languages,
            Type = CvSectionTypes.Languages,
            Title = "Languages",
            Placement = CvSectionPlacements.Sidebar,
            IsVisible = data.Languages.Count > 0,
            Order = order++,
            Items = data.Languages.Select(language => new CvSectionItem
            {
                PrimaryText = language
            }).ToList()
        });

        return sections;
    }

    public static List<CvSection> NormalizeSections(List<CvSection>? sections)
    {
        var normalized = new List<CvSection>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawSection in sections ?? new List<CvSection>())
        {
            var id = CleanText(rawSection.Id);
            var type = CleanText(rawSection.Type);
            var title = CleanText(rawSection.Title);

            if (string.IsNullOrWhiteSpace(id))
                id = Slugify(!string.IsNullOrWhiteSpace(title) ? title : type);

            if (string.IsNullOrWhiteSpace(type))
                type = id.StartsWith("custom", StringComparison.OrdinalIgnoreCase)
                    ? CvSectionTypes.Custom
                    : id;

            if (string.IsNullOrWhiteSpace(title))
                title = Humanize(type);

            if (!seen.Add(id))
                continue;

            normalized.Add(new CvSection
            {
                Id = id,
                Type = type,
                Title = title,
                Placement = NormalizePlacement(rawSection.Placement, type),
                IsVisible = rawSection.IsVisible,
                Order = rawSection.Order,
                Text = NullIfEmpty(rawSection.Text),
                Items = NormalizeSectionItems(rawSection.Items)
            });
        }

        return normalized
            .OrderBy(section => section.Order)
            .ThenBy(section => section.Title, StringComparer.OrdinalIgnoreCase)
            .Select((section, index) =>
            {
                section.Order = index;
                return section;
            })
            .ToList();
    }

    public static void ApplySectionsToLegacy(CvData data)
    {
        var sections = data.Sections
            .OrderBy(section => section.Order)
            .ToList();

        var summarySection = sections.FirstOrDefault(section => section.Type == CvSectionTypes.Summary);
        data.Summary = summarySection?.Text;

        data.Experience = sections
            .Where(section => section.Type == CvSectionTypes.Experience)
            .SelectMany(section => section.Items)
            .Select(item => new CvExperience
            {
                Role = item.PrimaryText,
                Company = item.SecondaryText,
                Start = item.StartDate,
                End = item.EndDate,
                Bullets = item.Bullets.ToList()
            })
            .ToList();

        data.Education = sections
            .Where(section => section.Type == CvSectionTypes.Education)
            .SelectMany(section => section.Items)
            .Select(item => new CvEducation
            {
                Degree = item.PrimaryText,
                Institution = item.SecondaryText,
                StartYear = item.StartDate,
                EndYear = item.EndDate,
                Year = item.Description ?? string.Empty
            })
            .ToList();

        data.Skills = sections
            .Where(section => section.Type == CvSectionTypes.Skills || section.Type == CvSectionTypes.SoftSkills)
            .SelectMany(section => section.Items)
            .Select(item => new CvSkill
            {
                Name = item.PrimaryText,
                Level = item.Level ?? 1,
                IsMatched = item.IsMatched,
                Category = sections.FirstOrDefault(s => s.Items.Contains(item))?.Type == CvSectionTypes.SoftSkills ? "Soft Skills" : null,
                TypeCompetence = sections.FirstOrDefault(s => s.Items.Contains(item))?.Type == CvSectionTypes.SoftSkills ? "Comportemental" : null
            })
            .ToList();

        data.Projects = sections
            .Where(section => section.Type == CvSectionTypes.Projects)
            .SelectMany(section => section.Items)
            .Select(item => new CvProject
            {
                Title = item.PrimaryText,
                Description = item.Description,
                Technologies = (item.SecondaryText ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
                DateRealisation = item.StartDate,
                Bullets = item.Bullets.ToList()
            })
            .ToList();

        data.Certifications = sections
            .Where(section => section.Type == CvSectionTypes.Certifications)
            .SelectMany(section => section.Items)
            .Select(item => item.PrimaryText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        data.Languages = sections
            .Where(section => section.Type == CvSectionTypes.Languages)
            .SelectMany(section => section.Items)
            .Select(item => item.PrimaryText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        data.Activities = sections
            .Where(section => section.Type == CvSectionTypes.Activities)
            .SelectMany(section => section.Items)
            .Select(item => new CvActivity
            {
                Title = item.PrimaryText,
                Role = NullIfEmpty(item.SecondaryText),
                Description = item.Description,
                StartDate = item.StartDate,
                EndDate = item.EndDate
            })
            .ToList();
    }

    public static IEnumerable<CvSection> VisibleSections(CvData data, params string[] placements)
    {
        var placementSet = placements.Length == 0
            ? null
            : new HashSet<string>(placements, StringComparer.OrdinalIgnoreCase);

        return data.Sections
            .Where(section => section.IsVisible)
            .Where(section => placementSet is null || placementSet.Contains(section.Placement))
            .OrderBy(section => section.Order);
    }

    private static List<CvSectionItem> NormalizeSectionItems(List<CvSectionItem>? items)
    {
        return (items ?? new List<CvSectionItem>())
            .Select(item => new CvSectionItem
            {
                PrimaryText = CleanText(item.PrimaryText),
                SecondaryText = CleanText(item.SecondaryText),
                StartDate = NullIfEmpty(item.StartDate),
                EndDate = NullIfEmpty(item.EndDate),
                Location = NullIfEmpty(item.Location),
                Description = NullIfEmpty(item.Description),
                Level = item.Level is null ? null : Math.Clamp(item.Level.Value, 1, 5),
                IsMatched = item.IsMatched,
                Bullets = item.Bullets
                    .Select(CleanText)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.PrimaryText) ||
                !string.IsNullOrWhiteSpace(item.SecondaryText) ||
                !string.IsNullOrWhiteSpace(item.Description) ||
                item.Bullets.Count > 0)
            .ToList();
    }

    private static string NormalizePlacement(string? placement, string type)
    {
        var clean = CleanText(placement).ToLowerInvariant();
        if (clean == CvSectionPlacements.Main || clean == CvSectionPlacements.Sidebar)
            return clean;

        return type switch
        {
            CvSectionTypes.Skills => CvSectionPlacements.Sidebar,
            CvSectionTypes.SoftSkills => CvSectionPlacements.Sidebar,
            CvSectionTypes.Education => CvSectionPlacements.Sidebar,
            CvSectionTypes.Languages => CvSectionPlacements.Sidebar,
            _ => CvSectionPlacements.Main
        };
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Section";

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            value.Replace("-", " ").Replace("_", " ").Trim());
    }

    private static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string? NullIfEmpty(string? value)
    {
        var clean = CleanText(value);
        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "custom-section";

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
                builder.Append(char.ToLowerInvariant(ch));
            else if (builder.Length == 0 || builder[^1] != '-')
                builder.Append('-');
        }

        return builder.ToString().Trim('-');
    }
}
