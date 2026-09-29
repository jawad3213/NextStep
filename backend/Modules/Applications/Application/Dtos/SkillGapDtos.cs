using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Applications.Application.Dtos;

/// <summary>POST /api/offers/skill-gap: the offer text to compare with the user's profile.</summary>
public class SkillGapRequestDto
{
    [Required]
    [MinLength(50, ErrorMessage = "The offer text must contain at least 50 characters.")]
    public string OfferText { get; set; } = string.Empty;
}

/// <summary>Skill-gap analysis of an offer against the user's profile (not stored).</summary>
public sealed record SkillGapResultDto
{
    public string CandidateName { get; init; } = "";
    public string JobTitle { get; init; } = "";

    /// <summary>Matching score, 0–100.</summary>
    public int RelevanceScore { get; init; }

    public List<SkillGapSkillDto> MatchedSkills { get; init; } = [];
    public List<SkillGapMissingSkillDto> MissingSkills { get; init; } = [];
    public List<string> RequiredCerts { get; init; } = [];
    public bool CertMatch { get; init; }
    public double ExperienceYears { get; init; }
    public double RequiredYears { get; init; }
    public double ExperienceGapYears { get; init; }

    /// <summary>PERFECT, MINOR or CRITICAL.</summary>
    public string Flag { get; init; } = SkillGapFlags.Minor;

    public List<SkillGapRecommendationDto> Recommendations { get; init; } = [];
    public List<string> RevisionHints { get; init; } = [];
}

public static class SkillGapFlags
{
    public const string Perfect = "PERFECT";
    public const string Minor = "MINOR";
    public const string Critical = "CRITICAL";
}

/// <param name="Category">"required", "preferred" or "keyword".</param>
public sealed record SkillGapSkillDto(string Name, string Category);

/// <param name="Priority">"high" (required by the offer) or "medium" (nice to have).</param>
public sealed record SkillGapMissingSkillDto(string Name, string Category, string Priority);

/// <param name="Type">SKILL or CERTIFICATION.</param>
public sealed record SkillGapRecommendationDto(string Type, string Title, string Description, string Priority);
