// ============================================================
// Modules/Offer/Services/OfferService.cs
// Logique métier : soumet l'offre au pipeline Python, sauvegarde en DB
// ============================================================
using System.Text.Json;
using NextStep.Shared.Http;
using NextStep.Modules.Offer.DTOs;
using NextStep.Modules.Offer.Models;
using NextStep.Modules.Offer.Repositories;
using NextStep.data;
using NextStep.Modules.Candidature.Models;

namespace NextStep.Modules.Offer.Services;

public interface IOfferService
{
    Task<OfferAnalysisDto> SubmitAndAnalyzeAsync(
        string rawText,
        int templateId,
        string userId,
        CancellationToken ct = default);

    Task<OfferAnalysisDto?> GetAnalysisAsync(Guid offerId, CancellationToken ct = default);
}

public class OfferService(
    IOfferRepository repository,
    IAgentHttpClient agentClient,
    AppDbContext db,
    ILogger<OfferService> logger) : IOfferService
{
    /// <summary>
    /// 1. Sauvegarde l'offre brute en DB
    /// 2. Lance le pipeline Python (/run-pipeline)
    /// 3. Met à jour l'offre avec le JSON analyse
    /// 4. Retourne le DTO complet vers Angular
    /// </summary>
    public async Task<OfferAnalysisDto> SubmitAndAnalyzeAsync(
        string rawText,
        int templateId,
        string userId,
        CancellationToken ct = default)
    {
        logger.LogInformation("OfferService — Soumission offre par user {UserId}", userId);

        Guid userGuid = Guid.TryParse(userId, out var parsedGuid) ? parsedGuid : Guid.Empty;

        // Étape 1 : Persiste l'offre brute
        var offre = new OffreEmploi
        {
            TexteBrut = rawText,
            UtilisateurId = userGuid,
        };
        offre = await repository.SaveAsync(offre, ct);

        // Étape 2 : Appel au pipeline Python
        JsonDocument pipelineResult;
        try
        {
            pipelineResult = await agentClient.RunPipelineAsync(rawText, userId, templateId, offre.Id.ToString(), ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "OfferService — Erreur appel pipeline Python");
            throw new InvalidOperationException("Le service IA est temporairement indisponible.", ex);
        }

        var root = pipelineResult.RootElement;

        // Étape 3 : Sauvegarde le JSON d'analyse
        var analyzeJson = root.TryGetProperty("analyzed_offer", out var ao)
            ? ao.GetRawText()
            : "{}";
        await repository.UpdateAnalyseJsonAsync(offre.Id, analyzeJson, ct);

        // Étape 3.1 : Sauvegarde le CV généré par l'IA (dans table document_genere)
        var candidature = new NextStep.Modules.Candidature.Models.Candidature
        {
            IdUtilisateur = userGuid,
            IdOffre = offre.Id,
            Statut = "EN_ATTENTE",
            DateCreation = DateTime.UtcNow
        };
        db.Candidatures.Add(candidature);

        var cvDataJson = root.TryGetProperty("cv_data", out var cd)
            ? cd.GetRawText()
            : null;

        var documentGenere = new DocumentGenere
        {
            IdCandidature = candidature.IdCandidature,
            CvContenuIaJson = cvDataJson,
            Version = 1,
            DateGeneration = DateTime.UtcNow
        };
        db.DocumentsGeneres.Add(documentGenere);

        await db.SaveChangesAsync(ct);

        // Étape 4 : Construit le DTO de retour
        var dto = MapToDto(offre.Id, root);
        logger.LogInformation(
            "OfferService — ✅ Pipeline terminé : matching={Matching}% ATS={ATS}%",
            dto.ScoreMatching, dto.ScoreAts);

        return dto;
    }

    public async Task<OfferAnalysisDto?> GetAnalysisAsync(Guid offerId, CancellationToken ct = default)
    {
        var offre = await repository.GetByIdAsync(offerId, ct);
        if (offre is null) return null;
        if (string.IsNullOrEmpty(offre.AnalyseJson)) return null;

        var doc = JsonDocument.Parse(offre.AnalyseJson);
        return MapToDto(offre.Id, doc.RootElement);
    }

    // ─── Mapping JSON Python → DTO .NET ───
    private static OfferAnalysisDto MapToDto(Guid offerId, JsonElement root)
    {
        var dto = new OfferAnalysisDto { OfferId = offerId };

        // Analyse offre (Agent 1)
        if (root.TryGetProperty("analyzed_offer", out var ao) && ao.ValueKind == JsonValueKind.Object)
        {
            dto.Titre = ao.GetStringOrDefault("titre") ?? "";
            dto.Entreprise = ao.GetStringOrDefault("entreprise");
            dto.TypeContrat = ao.GetStringOrDefault("type_contrat");
            dto.Localisation = ao.GetStringOrDefault("localisation");
            dto.DescriptionPoste = ao.GetStringOrDefault("description_poste");
            dto.AnneesExperience = ao.GetIntOrDefault("annees_experience");
            dto.NiveauEtudes = ao.GetStringOrDefault("niveau_etudes");
            dto.CompetencesRequises = ao.GetStringList("competences_requises");
            dto.CompetencesSouhaitees = ao.GetStringList("competences_souhaitees");
            dto.KeywordsAts = ao.GetStringList("keywords_ats");
        }

        // Scoring (Agent 4)
        if (root.TryGetProperty("match_result", out var mr) && mr.ValueKind == JsonValueKind.Object)
        {
            dto.ScoreMatching = mr.GetIntOrDefault("score_matching") ?? 0;
            dto.ScoreAts = mr.GetIntOrDefault("score_ats") ?? 0;
            dto.KeywordsPresents = mr.GetStringList("keywords_presents");
            dto.KeywordsManquants = mr.GetStringList("keywords_manquants");
            dto.Recommandations = mr.GetStringList("recommandations");
            dto.CompetencesMatching = mr.GetStringList("competences_matching");
            dto.CompetencesManquantes = mr.GetStringList("competences_manquantes");
        }

        // Erreurs pipeline
        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            dto.Erreurs = [.. errors.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)];
        }

        return dto;
    }
}

// ─── Extensions JSON helper ───
file static class JsonElementExtensions
{
    public static string? GetStringOrDefault(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public static int? GetIntOrDefault(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32()
            : null;

    public static List<string> GetStringList(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? [.. arr.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)]
            : [];
}
