// ============================================================
// Modules/Offer/DTOs/OfferAnalysisDto.cs
// Résultat de l'analyse IA retourné à Angular
// ============================================================
namespace NextStep.Modules.Offer.DTOs;

/// <summary>
/// Résultat complet retourné après le lancement du pipeline IA.
/// Reflète le JSON produit par les Agents 1-4 (Python).
/// </summary>
public class OfferAnalysisDto
{
    public Guid OfferId { get; set; }

    // ─── Agent 1 : Analyse offre ───
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

    // ─── Métadonnées ───
    public DateTime DateAnalyse { get; set; } = DateTime.UtcNow;
    public List<string> Erreurs { get; set; } = [];
}

public class CompanyNewsItem
{
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}
