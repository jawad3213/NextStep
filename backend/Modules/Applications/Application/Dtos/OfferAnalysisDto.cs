// ============================================================
// Modules/Applications/DTOs/OfferAnalysisDto.cs
// AI analysis result returned to Angular
// ============================================================
namespace NextStep.Modules.Applications.Application.Dtos;

/// <summary>
/// Complete result returned after running the AI pipeline.
/// Reflects the JSON produced by Agents 1-4 (Python).
/// </summary>
public class OfferAnalysisDto
{
    public Guid OfferId { get; set; }

    // ─── Agent 1 : Offer Analysis ───
    public string Titre { get; set; } = string.Empty;
    public string? Entreprise { get; set; }
    public string? TypeContrat { get; set; }
    public string? Localisation { get; set; }
    public List<string> CompetencesRequises { get; set; } = [];
    public List<string> CompetencesSouhaitees { get; set; } = [];
    public List<string> KeywordsAts { get; set; } = [];
    public int? AnneesExperience { get; set; }
    public string? NiveauEtudes { get; set; }
    public string? ModeTravail { get; set; }
    public string? DescriptionPoste { get; set; }
    public string? TexteBrut { get; set; }

    // ─── Agent 4 : Scoring ───
    public int ScoreMatching { get; set; }
    public int ScoreAts { get; set; }
    public List<string> KeywordsPresents { get; set; } = [];
    public List<string> KeywordsManquants { get; set; } = [];
    public List<string> Recommandations { get; set; } = [];
    public List<string> CompetencesMatching { get; set; } = [];
    public List<string> CompetencesManquantes { get; set; } = [];

    // ─── Agent 4 : Company Intelligence ───
    public double CompanyCultureScore { get; set; }
    public int CompanySalaryMin { get; set; }
    public int CompanySalaryMax { get; set; }
    public string CompanySize { get; set; } = string.Empty;
    public List<CompanyNewsItem> CompanyNews { get; set; } = [];

    // ─── Metadata ───
    public DateTime DateAnalyse { get; set; } = DateTime.UtcNow;
    public List<string> Erreurs { get; set; } = [];

    /// <summary>
    /// Identifies the run that produced the stored analysis. DateAnalyse is the read time,
    /// so it cannot tell a fresh result from the one already on the offer; this lets a
    /// client waiting on a background run detect the new result even when the outcome
    /// is identical to the previous one.
    /// </summary>
    public string? RunId { get; set; }

    // Agents CV Optimizer + CV Engine
    // This is the canonical generated CV payload used by the editor,
    // live backend PDF preview, final save and download.
    public object? CvGeneratedContent { get; set; }
    public object? ProfileData { get; set; }
    public object? SkillGapAnalysis { get; set; }
    public object? MatchResult { get; set; }
}

public class CompanyNewsItem
{
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}
