using System.Text.Json;
using NextStep.Modules.Offer.DTOs;
using NextStep.Modules.Offer.Models;
using NextStep.Modules.Offer.Repositories;
using NextStep.data;
using NextStep.Modules.Candidature.Models;

namespace NextStep.Modules.Offer.Services;

public interface IOfferService
{
    Task<OffreEmploi> SaveOfferAsync(string rawText, string userId, CancellationToken ct = default);
    Task<OfferAnalysisDto?> GetAnalysisAsync(Guid offerId, CancellationToken ct = default);
    Task SavePipelineResultAsync(Guid offerId, JsonDocument pipelineResult, Guid userId, CancellationToken ct = default);
    Task<OffreEmploi?> GetOfferWithAnalysisAsync(Guid offerId, CancellationToken ct = default);
}

public class OfferService(
    IOfferRepository repository,
    AppDbContext db,
    ILogger<OfferService> logger) : IOfferService
{
    public async Task<OffreEmploi> SaveOfferAsync(string rawText, string userId, CancellationToken ct = default)
    {
        Guid userGuid = Guid.TryParse(userId, out var parsedGuid) ? parsedGuid : Guid.Empty;

        var offre = new OffreEmploi
        {
            TexteBrut = rawText,
            UtilisateurId = userGuid,
        };
        offre = await repository.SaveAsync(offre, ct);
        logger.LogInformation("OfferService — Offre sauvegardée {OfferId}", offre.Id);
        return offre;
    }

    public async Task SavePipelineResultAsync(Guid offerId, JsonDocument pipelineResult, Guid userId, CancellationToken ct = default)
    {
        var root = pipelineResult.RootElement;
        var fullJson = root.GetRawText();

        await repository.UpdateAnalyseJsonAsync(offerId, fullJson, ct);

        var candidature = new NextStep.Modules.Candidature.Models.Candidature
        {
            IdUtilisateur = userId,
            IdOffre = offerId,
            Statut = "EN_ATTENTE",
            DateCreation = DateTime.UtcNow
        };
        db.Candidatures.Add(candidature);
        await db.SaveChangesAsync(ct);

        var cvDataJson = root.TryGetProperty("cv_data", out var cd)
            ? cd.GetRawText()
            : null;

        if (cvDataJson != null)
        {
            var documentGenere = new DocumentGenere
            {
                IdCandidature = candidature.IdCandidature,
                CvContenuIaJson = cvDataJson,
                Version = 1,
                DateGeneration = DateTime.UtcNow
            };
            db.DocumentsGeneres.Add(documentGenere);
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("OfferService — Résultats pipeline sauvegardés pour offre {OfferId}", offerId);
    }

    public async Task<OfferAnalysisDto?> GetAnalysisAsync(Guid offerId, CancellationToken ct = default)
    {
        var offre = await repository.GetByIdAsync(offerId, ct);
        if (offre is null) return null;
        if (string.IsNullOrEmpty(offre.AnalyseJson)) return null;

        var doc = JsonDocument.Parse(offre.AnalyseJson);
        return MapToDto(offre.Id, doc.RootElement);
    }

    private static OfferAnalysisDto MapToDto(Guid offerId, JsonElement root)
    {
        var dto = new OfferAnalysisDto { OfferId = offerId };

        if (root.TryGetProperty("analyzed_offer", out var ao) && ao.ValueKind == JsonValueKind.Object)
        {
            dto.Titre = ao.GetStringOrDefault("titre") ?? "";
            dto.Entreprise = ao.GetStringOrDefault("entreprise");
            dto.TypeContrat = ao.GetStringOrDefault("type_contrat");
            dto.Localisation = ao.GetStringOrDefault("localisation");
            dto.DescriptionPoste = ao.GetStringOrDefault("description_poste");
            dto.AnneesExperience = ao.GetStringAsIntOrDefault("annees_experience");
            dto.NiveauEtudes = ao.GetStringOrDefault("niveau_etudes");
            dto.CompetencesRequises = ao.GetStringList("competences_requises");
            dto.CompetencesSouhaitees = ao.GetStringList("competences_souhaitees");
            dto.KeywordsAts = ao.GetStringList("keywords_ats");
        }

        if (root.TryGetProperty("match_result", out var mr) && mr.ValueKind == JsonValueKind.Object)
        {
            dto.ScoreMatching = mr.GetIntOrDefault("score_matching") ?? mr.GetIntOrDefault("match_score") ?? dto.ScoreMatching;
            dto.ScoreAts = mr.GetIntOrDefault("score_ats") ?? mr.GetIntOrDefault("ats_score") ?? dto.ScoreAts;
            
            var mrKeywordsPresents = mr.GetStringList("keywords_presents");
            if (mrKeywordsPresents.Count > 0) dto.KeywordsPresents = mrKeywordsPresents;
            
            var mrKeywordsManquants = mr.GetStringList("keywords_manquants");
            if (mrKeywordsManquants.Count > 0) dto.KeywordsManquants = mrKeywordsManquants;
            
            var mrRecommandations = mr.GetStringList("recommandations");
            if (mrRecommandations.Count > 0) dto.Recommandations = mrRecommandations;
            
            var mrCompetencesMatching = mr.GetStringList("competences_matching");
            if (mrCompetencesMatching.Count > 0) dto.CompetencesMatching = mrCompetencesMatching;
            
            var mrCompetencesManquantes = mr.GetStringList("competences_manquantes");
            if (mrCompetencesManquantes.Count > 0) dto.CompetencesManquantes = mrCompetencesManquantes;
        }

        if (root.TryGetProperty("skill_gap", out var sg) && sg.ValueKind == JsonValueKind.Object)
        {
            dto.ScoreMatching = sg.GetIntOrDefault("score_matching") ?? sg.GetIntOrDefault("match_score") ?? dto.ScoreMatching;
            dto.ScoreAts = sg.GetIntOrDefault("score_ats") ?? sg.GetIntOrDefault("ats_score") ?? dto.ScoreAts;
            
            var sgKeywordsPresents = sg.GetStringList("keywords_presents");
            if (sgKeywordsPresents.Count > 0) dto.KeywordsPresents = sgKeywordsPresents;
            
            var sgKeywordsManquants = sg.GetStringList("keywords_manquants");
            if (sgKeywordsManquants.Count > 0) dto.KeywordsManquants = sgKeywordsManquants;
            
            var sgRecommandations = sg.GetStringList("recommandations");
            if (sgRecommandations.Count > 0) dto.Recommandations = sgRecommandations;
            
            var sgCompetencesMatching = sg.GetStringList("competences_matching");
            if (sgCompetencesMatching.Count > 0)
            {
                dto.CompetencesMatching = sgCompetencesMatching;
                if (dto.KeywordsPresents.Count == 0) dto.KeywordsPresents = sgCompetencesMatching;
            }
            
            var sgCompetencesManquantes = sg.GetStringList("competences_manquantes");
            if (sgCompetencesManquantes.Count > 0)
            {
                dto.CompetencesManquantes = sgCompetencesManquantes;
                if (dto.KeywordsManquants.Count == 0) dto.KeywordsManquants = sgCompetencesManquantes;
            }
            
            // Fallback: use matched_skills / missing_skills if competence_* are empty
            var sgMatched = sg.GetStringList("matched_skills");
            if (sgMatched.Count > 0 && dto.CompetencesMatching.Count == 0)
            {
                dto.CompetencesMatching = sgMatched;
                if (dto.KeywordsPresents.Count == 0) dto.KeywordsPresents = sgMatched;
            }
            
            var sgMissing = sg.GetStringList("missing_skills");
            if (sgMissing.Count > 0 && dto.CompetencesManquantes.Count == 0)
            {
                dto.CompetencesManquantes = sgMissing;
                if (dto.KeywordsManquants.Count == 0) dto.KeywordsManquants = sgMissing;
            }
        }

        // ── Fallback déterministe si skill_gap est vide/absent ──
        if (dto.ScoreMatching == 0 && dto.CompetencesMatching.Count == 0 && dto.CompetencesRequises.Count > 0)
        {
            var allSkills = new List<string>();
            var matched = new List<string>();
            var missing = new List<string>();

            if (root.TryGetProperty("profile_data", out var pd) && pd.ValueKind == JsonValueKind.Object)
            {
                var comps = pd.GetPropertyOrNull("competences");
                if (comps.HasValue && comps.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in comps.Value.EnumerateArray())
                    {
                        var nom = c.GetStringOrDefault("nom");
                        if (nom != null) allSkills.Add(nom.ToLowerInvariant().Trim());
                    }
                }

                var rawSkills = pd.GetPropertyOrNull("skills");
                if (rawSkills.HasValue && rawSkills.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in rawSkills.Value.EnumerateArray())
                    {
                        if (s.ValueKind == JsonValueKind.String)
                            allSkills.Add(s.GetString()!.ToLowerInvariant().Trim());
                        else if (s.ValueKind == JsonValueKind.Object)
                        {
                            var n = s.GetStringOrDefault("nom") ?? s.GetStringOrDefault("name");
                            if (n != null) allSkills.Add(n.ToLowerInvariant().Trim());
                        }
                    }
                }
            }

            if (allSkills.Count == 0)
            {
                // Si on n'a pas le profil, on ne peut pas matcher
                dto.Recommandations = dto.CompetencesRequises.Select(r => $"Ajouter '{r}' à votre profil").ToList();
            }
            else
            {
                foreach (var req in dto.CompetencesRequises)
                {
                    var reqLower = req.ToLowerInvariant().Trim();
                    var reqWords = reqLower.Split(new[] { ' ', '-', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

                    bool found = allSkills.Any(s =>
                        s == reqLower
                        || s.Contains(reqLower)
                        || reqLower.Contains(s)
                        || reqWords.Any(w => w.Length > 2 && s.Contains(w))
                        || reqWords.Any(w => w.Length > 2 && s.Split(new[] { ' ', '-', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries).Contains(w))
                    );

                    if (found)
                        matched.Add(req);
                    else
                        missing.Add(req);
                }

                if (matched.Count + missing.Count > 0)
                {
                    dto.ScoreMatching = (int)Math.Round((double)matched.Count / (matched.Count + missing.Count) * 100);
                    dto.CompetencesMatching = matched;
                    dto.CompetencesManquantes = missing;
                    dto.KeywordsPresents = matched;
                    dto.KeywordsManquants = missing;
                    dto.Recommandations = missing.Select(m => $"Ajouter '{m}' à votre profil").ToList();
                }
            }
        }

        if (root.TryGetProperty("company_intelligence", out var ci) && ci.ValueKind == JsonValueKind.Object)
        {
            var intelligence = ci.ValueKind == JsonValueKind.Object && ci.TryGetProperty("intelligence", out var i) ? i : ci;
            
            var culture = intelligence.GetPropertyOrNull("culture");
            if (culture.HasValue)
            {
                dto.CompanyCultureScore = culture.Value.GetDoubleOrDefault("glassdoor_rating") ?? culture.Value.GetDoubleOrDefault("culture_score") ?? 0;
            }

            var salaries = intelligence.GetPropertyOrNull("salaries");
            if (salaries.HasValue && salaries.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in salaries.Value.EnumerateArray())
                {
                    dto.CompanySalaryMin = s.GetIntOrDefault("min_salary") ?? dto.CompanySalaryMin;
                    dto.CompanySalaryMax = s.GetIntOrDefault("max_salary") ?? dto.CompanySalaryMax;
                }
            }

            var actualites = intelligence.GetPropertyOrNull("actualites");
            if (actualites.HasValue && actualites.Value.ValueKind == JsonValueKind.Array)
            {
                dto.CompanyNews = [.. actualites.Value.EnumerateArray()
                    .Where(a => a.ValueKind == JsonValueKind.String)
                    .Select(a => new CompanyNewsItem { Title = a.GetString() ?? "", Date = "" })];
            }
        }

        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            dto.Erreurs = [.. errors.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)];
        }

        return dto;
    }

    public async Task<OffreEmploi?> GetOfferWithAnalysisAsync(Guid offerId, CancellationToken ct = default)
    {
        return await repository.GetByIdWithAnalysisAsync(offerId, ct);
    }
}

public static class JsonElementExtensions
{
    public static string? GetStringOrDefault(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public static int? GetIntOrDefault(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Number)
            return null;
            
        // Use TryGetInt32 first, fallback to GetDouble and cast if it's a float
        if (v.TryGetInt32(out var i)) return i;
        return (int)v.GetDouble();
    }

    public static int? GetStringAsIntOrDefault(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
        if (v.ValueKind == JsonValueKind.String)
        {
            var str = v.GetString();
            if (string.IsNullOrEmpty(str)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(str, @"\d+");
            if (match.Success && int.TryParse(match.Value, out var parsed))
                return parsed;
        }
        return null;
    }

    public static List<string> GetStringList(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? [.. arr.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)]
            : [];

    public static double? GetDoubleOrDefault(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : null;

    public static JsonElement? GetPropertyOrNull(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? v : null;
}
