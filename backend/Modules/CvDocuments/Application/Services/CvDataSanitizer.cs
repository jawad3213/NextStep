using NextStep.Modules.CvDocuments.Infrastructure.Rendering;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NextStep.Modules.CvDocuments.Domain;

namespace NextStep.Modules.CvDocuments.Application.Services;

/// <summary>
/// Normalises CV content before it is stored or rendered: trims text, removes duplicate
/// skills/activities/projects, merges skill sources and separates activities from experience.
/// Pure functions, shared by the service, the HTML renderer and the PDF templates.
/// </summary>
public static class CvDataSanitizer
{
    private static readonly string[] ActivitySignals =
    {
        "hackathon", "club", "association", "organisateur", "organizer",
        "membre", "member", "volunteer", "benevole", "event", "community",
        "it day", "prize", "prix", "participant", "formateur", "trainer",
        "formation", "solihackathon", "itwave", "ids"
    };

    public static CvData Sanitize(CvData data)
    {
        data ??= new CvData();
        data.Experience ??= new List<CvExperience>();
        data.Education ??= new List<CvEducation>();
        data.Activities ??= new List<CvActivity>();
        data.Projects ??= new List<CvProject>();
        data.Skills ??= new List<CvSkill>();
        data.TechnicalSkills ??= new List<CvSkill>();
        data.SoftSkills ??= new List<CvSkill>();
        data.Certifications ??= new List<string>();
        data.Languages ??= new List<string>();
        data.Sections ??= new List<CvSection>();

        data.Skills = MergeSkillSources(data.Skills, data.TechnicalSkills, data.SoftSkills);
        data.Experience = NormalizeExperience(data.Experience, data.Activities);
        data.Activities = DeduplicateActivities(data.Activities);
        data.Projects = NormalizeProjects(data.Projects);
        data.Skills = DeduplicateSkills(data.Skills).ToList();
        data.Certifications = DeduplicateStrings(data.Certifications).ToList();
        data.Languages = DeduplicateStrings(data.Languages).ToList();

        data.Sections = CvSectionMapper.MergeWithLegacySections(data, data.Sections);
        return data;
    }

    private static List<CvExperience> NormalizeExperience(List<CvExperience>? experiences, List<CvActivity>? activities)
    {
        var clean = new List<CvExperience>();
        var seen = new HashSet<string>();
        activities ??= new List<CvActivity>();

        foreach (var exp in experiences ?? new List<CvExperience>())
        {
            exp.Role = CleanText(exp.Role);
            exp.Company = CleanText(exp.Company);
            exp.Start = NullIfEmpty(exp.Start);
            exp.End = NullIfEmpty(exp.End);
            exp.Bullets = DeduplicateStrings(exp.Bullets).ToList();

            if (string.IsNullOrWhiteSpace(exp.Role) && string.IsNullOrWhiteSpace(exp.Company))
                continue;

            if (LooksLikeActivity(exp))
            {
                activities.Add(new CvActivity
                {
                    Title = string.IsNullOrWhiteSpace(exp.Company) ? exp.Role : exp.Company,
                    Role = string.IsNullOrWhiteSpace(exp.Company) ? null : exp.Role,
                    Description = exp.Bullets.FirstOrDefault()
                });
                continue;
            }

            var key = NormalizeKey($"{exp.Role}|{exp.Company}");
            if (!seen.Add(key)) continue;
            clean.Add(exp);
        }

        return clean;
    }

    private static List<CvProject> NormalizeProjects(List<CvProject>? projects)
    {
        var clean = new List<CvProject>();
        var seen = new HashSet<string>();

        foreach (var project in projects ?? new List<CvProject>())
        {
            project.Title = CleanText(project.Title);
            project.Description = NullIfEmpty(project.Description);
            project.DateRealisation = NullIfEmpty(project.DateRealisation);
            project.Technologies = DeduplicateStrings(project.Technologies).ToList();
            project.Bullets = DeduplicateStrings(project.Bullets).ToList();

            if (string.IsNullOrWhiteSpace(project.Title))
                continue;

            var key = NormalizeKey(project.Title);
            if (!seen.Add(key)) continue;

            project.Bullets = project.Bullets
                .Where(b => !IsSameMeaning(b, project.Description))
                .ToList();

            clean.Add(project);
        }

        return clean;
    }

    private static List<CvActivity> DeduplicateActivities(List<CvActivity>? activities)
    {
        var clean = new List<CvActivity>();
        var seen = new HashSet<string>();

        foreach (var activity in activities ?? new List<CvActivity>())
        {
            activity.Title = CleanText(activity.Title);
            activity.Role = NullIfEmpty(activity.Role);
            activity.Description = NullIfEmpty(activity.Description);
            activity.StartDate = NullIfEmpty(activity.StartDate);
            activity.EndDate = NullIfEmpty(activity.EndDate);

            if (string.IsNullOrWhiteSpace(activity.Title) && string.IsNullOrWhiteSpace(activity.Role))
                continue;

            var key = NormalizeKey($"{activity.Role}|{activity.Title}");
            if (!seen.Add(key)) continue;
            clean.Add(activity);
        }

        return clean;
    }

    private static IEnumerable<string> DeduplicateStrings(IEnumerable<string>? values)
    {
        var seen = new HashSet<string>();
        foreach (var value in values ?? Enumerable.Empty<string>())
        {
            var clean = CleanText(value);
            if (string.IsNullOrWhiteSpace(clean)) continue;
            if (seen.Add(NormalizeKey(clean))) yield return clean;
        }
    }

    private static IEnumerable<CvSkill> DeduplicateSkills(IEnumerable<CvSkill>? skills)
    {
        var seen = new HashSet<string>();
        foreach (var skill in skills ?? Enumerable.Empty<CvSkill>())
        {
            skill.Name = CleanText(skill.Name);
            if (string.IsNullOrWhiteSpace(skill.Name)) continue;
            if (!seen.Add(NormalizeKey(skill.Name))) continue;
            skill.Level = Math.Clamp(skill.Level, 1, 5);
            yield return skill;
        }
    }

    private static List<CvSkill> MergeSkillSources(
        IEnumerable<CvSkill>? skills,
        IEnumerable<CvSkill>? technicalSkills,
        IEnumerable<CvSkill>? softSkills)
    {
        var merged = new List<CvSkill>();
        merged.AddRange(skills ?? Enumerable.Empty<CvSkill>());
        merged.AddRange(technicalSkills ?? Enumerable.Empty<CvSkill>());

        foreach (var skill in softSkills ?? Enumerable.Empty<CvSkill>())
        {
            if (string.IsNullOrWhiteSpace(skill.Category))
                skill.Category = "Soft Skills";
            if (string.IsNullOrWhiteSpace(skill.TypeCompetence))
                skill.TypeCompetence = "Comportemental";
            merged.Add(skill);
        }

        return merged;
    }

    private static bool LooksLikeActivity(CvExperience exp)
    {
        var text = NormalizeKey($"{exp.Role} {exp.Company} {string.Join(" ", exp.Bullets ?? new List<string>())}");
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (NormalizeKey(exp.Role).Contains("stage") || NormalizeKey(exp.Role).Contains("intern"))
            return false;
        return ActivitySignals.Any(signal => text.Contains(signal));
    }

    private static bool IsSameMeaning(string? left, string? right)
    {
        var a = NormalizeKey(left);
        var b = NormalizeKey(right);
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        return a == b || a.Contains(b) || b.Contains(a);
    }

    private static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string? NullIfEmpty(string? value)
    {
        var clean = CleanText(value);
        return string.IsNullOrWhiteSpace(clean) ? null : clean;
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(ch));
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"[^a-z0-9]+", " ").Trim();
    }
}
