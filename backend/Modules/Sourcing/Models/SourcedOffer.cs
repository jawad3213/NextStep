using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Sourcing.Models;

[Table("sourced_offer")]
public class SourcedOffer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("provider")]
    public string Provider { get; set; } = string.Empty;

    [Column("provider_job_id")]
    public string? ProviderJobId { get; set; }

    [Column("external_url")]
    public string? ExternalUrl { get; set; }

    [Required]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("company")]
    public string? Company { get; set; }

    [Column("location")]
    public string? Location { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("posted_at_text")]
    public string? PostedAtText { get; set; }

    [Column("posted_window")]
    public string? PostedWindow { get; set; }

    [Column("raw_contract_type")]
    public string? RawContractType { get; set; }

    [Column("normalized_contract_type")]
    public string? NormalizedContractType { get; set; }

    [Column("employment_type")]
    public string? EmploymentType { get; set; }

    [Column("seniority_level")]
    public string? SeniorityLevel { get; set; }

    [Column("matched_it_terms_json", TypeName = "jsonb")]
    public string MatchedItTermsJson { get; set; } = "[]";

    [Column("ai_score")]
    public int? AiScore { get; set; }

    [Column("ai_score_skills")]
    public int? AiScoreSkills { get; set; }

    [Column("ai_score_title")]
    public int? AiScoreTitle { get; set; }

    [Column("ai_score_location")]
    public int? AiScoreLocation { get; set; }

    [Column("ai_score_contract")]
    public int? AiScoreContract { get; set; }

    [Column("ai_score_freshness")]
    public int? AiScoreFreshness { get; set; }

    [Column("ai_confidence")]
    public double? AiConfidence { get; set; }

    [Column("ai_matched_skills_json", TypeName = "jsonb")]
    public string AiMatchedSkillsJson { get; set; } = "[]";

    [Column("ai_missing_skills_json", TypeName = "jsonb")]
    public string AiMissingSkillsJson { get; set; } = "[]";

    [Column("ai_reasons_json", TypeName = "jsonb")]
    public string AiReasonsJson { get; set; } = "[]";

    [Column("ai_summary")]
    public string? AiSummary { get; set; }

    [Column("ai_ranked_at_utc")]
    public DateTime? AiRankedAtUtc { get; set; }

    [Column("source_query_json", TypeName = "jsonb")]
    public string SourceQueryJson { get; set; } = "{}";

    [Required]
    [Column("dedupe_key")]
    public string DedupeKey { get; set; } = string.Empty;

    [Column("is_saved")]
    public bool IsSaved { get; set; }

    [Column("is_shortlisted")]
    public bool IsShortlisted { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    [Column("promoted_offer_id")]
    public Guid? PromotedOfferId { get; set; }

    [Column("first_seen_at_utc")]
    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;

    [Column("last_seen_at_utc")]
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    [Column("scraped_at_utc")]
    public DateTime ScrapedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime? UpdatedAtUtc { get; set; }
}
