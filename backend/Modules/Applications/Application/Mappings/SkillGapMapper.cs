using System.Text.Json;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.Json;

namespace NextStep.Modules.Applications.Application.Mappings;

/// <summary>
/// Builds the skill-gap result from the agents' output (analysed offer + deterministic match)
/// and the candidate profile, which gives the experience years and certifications.
/// </summary>
public static class SkillGapMapper
{
    private const int MaxRecommendations = 5;
    private const int MaxKeywordHints = 5;

    public static SkillGapResultDto ToResult(
        JsonElement analyzedOffer,
        JsonElement match,
        CandidateProfile? candidate,
        DateTime today)
    {
        var required = ToKeySet(analyzedOffer.GetStringList("competences_requises"));
        var preferred = ToKeySet(analyzedOffer.GetStringList("competences_souhaitees"));
        string CategoryOf(string skill) =>
            required.Contains(Key(skill)) ? "required" : preferred.Contains(Key(skill)) ? "preferred" : "keyword";

        var matched = Distinct(match.GetStringList("matched_skills"))
            .Select(s => new SkillGapSkillDto(s, CategoryOf(s)))
            .ToList();

        // Skills the offer requires come first: they weigh most in the score.
        var missing = Distinct(match.GetStringList("missing_skills"))
            .Select(s => new SkillGapMissingSkillDto(s, CategoryOf(s), CategoryOf(s) == "required" ? "high" : "medium"))
            .OrderBy(s => s.Priority == "high" ? 0 : 1)
            .ToList();

        var score = Math.Clamp(match.GetIntOrDefault("score_matching") ?? 0, 0, 100);

        var experienceYears = candidate is null ? 0 : ExperienceYears(candidate.Experiences, today);
        var requiredYears = (double)(analyzedOffer.GetStringAsIntOrDefault("annees_experience") ?? 0);
        var gapYears = Math.Max(0, Math.Round(requiredYears - experienceYears, 1));

        var requiredCerts = Distinct(match.GetStringList("required_certs"));
        var candidateCerts = ToKeySet(candidate?.Certifications ?? []);
        var missingCerts = requiredCerts.Where(c => !candidateCerts.Contains(Key(c))).ToList();

        var highMissing = missing.Count(s => s.Priority == "high");
        var flag = score < 60 || gapYears >= 2
            ? SkillGapFlags.Critical
            : score >= 85 && highMissing == 0 && gapYears == 0 && missingCerts.Count == 0
                ? SkillGapFlags.Perfect
                : SkillGapFlags.Minor;

        return new SkillGapResultDto
        {
            CandidateName = candidate?.User.FullName is { Length: > 0 } name ? name : "Candidat",
            JobTitle = analyzedOffer.GetStringOrDefault("titre") ?? match.GetStringOrDefault("job_title") ?? "Poste",
            RelevanceScore = score,
            MatchedSkills = matched,
            MissingSkills = missing,
            RequiredCerts = requiredCerts,
            CertMatch = missingCerts.Count == 0,
            ExperienceYears = experienceYears,
            RequiredYears = requiredYears,
            ExperienceGapYears = gapYears,
            Flag = flag,
            Recommendations = Recommendations(missing, missingCerts, match),
            RevisionHints = RevisionHints(match, gapYears),
        };
    }

    /// <summary>Years worked, counting overlapping experiences once. An open experience runs until today.</summary>
    public static double ExperienceYears(IEnumerable<ExperienceSummary> experiences, DateTime today)
    {
        var periods = experiences
            .Where(e => e.StartDate.HasValue)
            .Select(e => (Start: e.StartDate!.Value, End: e.EndDate ?? today))
            .Where(p => p.End > p.Start)
            .OrderBy(p => p.Start)
            .ToList();

        var totalDays = 0.0;
        DateTime? start = null, end = null;
        foreach (var p in periods)
        {
            if (end is null || p.Start > end)
            {
                if (start is not null) totalDays += (end!.Value - start.Value).TotalDays;
                (start, end) = (p.Start, p.End);
            }
            else if (p.End > end)
            {
                end = p.End;
            }
        }
        if (start is not null) totalDays += (end!.Value - start.Value).TotalDays;

        return Math.Round(totalDays / 365.25, 1);
    }

    private static List<SkillGapRecommendationDto> Recommendations(
        List<SkillGapMissingSkillDto> missing, List<string> missingCerts, JsonElement match)
    {
        // The agents write one recommendation per missing skill, in the order of missing_skills.
        var agentTexts = Distinct(match.GetStringList("missing_skills"))
            .Zip(match.GetStringList("recommandations"))
            .ToDictionary(p => Key(p.First), p => p.Second);

        var recommendations = missing
            .Take(MaxRecommendations)
            .Select(s => new SkillGapRecommendationDto(
                "SKILL",
                s.Name,
                agentTexts.GetValueOrDefault(Key(s.Name))
                    ?? $"Add concrete proof of '{s.Name}' in an experience or project.",
                s.Priority))
            .ToList();

        recommendations.AddRange(missingCerts.Select(c => new SkillGapRecommendationDto(
            "CERTIFICATION", c, $"The job offer requires the certification '{c}'.", "medium")));
        return recommendations;
    }

    private static List<string> RevisionHints(JsonElement match, double gapYears)
    {
        var hints = Distinct(match.GetStringList("revision_hints"));

        foreach (var partial in Distinct(match.GetStringList("partial_skills")))
            hints.Add($"Clarify your level in {partial}: your profile only partially covers it.");

        foreach (var keyword in Distinct(match.GetStringList("keywords_manquants")).Take(MaxKeywordHints))
            hints.Add($"Include the keyword \"{keyword}\" in your resume if it matches your experience.");

        if (gapYears > 0)
            hints.Add($"You are missing approximately {gapYears} year(s) of experience: highlight internships, projects, and work-study programs.");

        return hints;
    }

    private static string Key(string value) => value.Trim().ToLowerInvariant();

    private static HashSet<string> ToKeySet(IEnumerable<string> values) => values.Select(Key).ToHashSet();

    private static List<string> Distinct(IEnumerable<string> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .DistinctBy(Key)
            .ToList();
}
