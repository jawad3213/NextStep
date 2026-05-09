// ============================================================
// Modules/Offer/Services/OfferService.cs
// Logique métier : soumet l'offre au pipeline Python, sauvegarde en DB
// ============================================================
using System.Text.Json;
using NextStep.Shared.Http;
using NextStep.Modules.Offer.DTOs;
using NextStep.Modules.Offer.Models;
using NextStep.Modules.Offer.Repositories;

namespace NextStep.Modules.Offer.Services;

public interface IOfferService
{
    Task<OfferAnalysisDto> SubmitAndAnalyzeAsync(
        string rawText,
        string? manualTitre,
        string? manualEntreprise,
        string templateId,
        string userId,
        CancellationToken ct = default);

    Task<OfferAnalysisDto?> GetAnalysisAsync(Guid offerId, CancellationToken ct = default);

    Task<List<OfferAnalysisDto>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}

public class OfferService(
    IOfferRepository repository,
    IAgentHttpClient agentClient,
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
        string? manualTitre,
        string? manualEntreprise,
        string templateId,
        string userId,
        CancellationToken ct = default)
    {
        logger.LogInformation("OfferService — Soumission offre par user {UserId}", userId);

        // Étape 1 : Persiste l'offre brute
        var offre = new OffreEmploi
        {
            TexteBrut = rawText,
            UtilisateurId = Guid.Parse(userId),
        };
        offre = await repository.SaveAsync(offre, ct);

        // Étape 2 : Appel au pipeline Python
        JsonDocument? pipelineResult = null;
        try
        {
            pipelineResult = await agentClient.RunPipelineAsync(rawText, userId, 1, ct); // Using 1 for template for now
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OfferService — AI Agent failed or unavailable. Using manual metadata.");
        }
        
        // Étape 3 : Sauvegarde le JSON d'analyse (AI ou Manuel)
        string analyzeJson;
        if (pipelineResult != null)
        {
            // On sauvegarde l'intégralité du résultat du pipeline (Analyse + Match + Company)
            // pour que les agents suivants (Email) puissent en bénéficier.
            analyzeJson = pipelineResult.RootElement.GetRawText();
        }
        else
        {
            // Create a manual analysis JSON if AI failed
            analyzeJson = JsonSerializer.Serialize(new {
                titre = manualTitre ?? "Offre sans titre",
                entreprise = manualEntreprise ?? "Entreprise inconnue",
                description_poste = rawText.Length > 200 ? rawText[..200] + "..." : rawText
            });
        }
        
        await repository.UpdateAnalyseJsonAsync(offre.Id, analyzeJson, ct);

        // Étape 4 : Construit le DTO de retour
        var finalDoc = JsonDocument.Parse(analyzeJson);
        var dto = MapToDto(offre.Id, finalDoc.RootElement);
        
        logger.LogInformation("OfferService — ✅ Offer saved: {Titre} | {Entreprise}", 
            dto.Titre, dto.Entreprise);

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

    public async Task<List<OfferAnalysisDto>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var offres = await repository.GetByUserIdAsync(userId, ct);
        return offres
            .Select(o =>
            {
                if (string.IsNullOrEmpty(o.AnalyseJson))
                {
                    // Fallback for offers without analysis yet
                    return new OfferAnalysisDto 
                    { 
                        OfferId = o.Id, 
                        Titre = "Offre en attente d'analyse",
                        DescriptionPoste = o.TexteBrut?.Length > 100 ? o.TexteBrut[..100] + "..." : o.TexteBrut
                    };
                }
                var doc = JsonDocument.Parse(o.AnalyseJson);
                return MapToDto(o.Id, doc.RootElement);
            })
            .ToList();
    }

    // ─── Mapping JSON Python → DTO .NET ───
    private static OfferAnalysisDto MapToDto(Guid offerId, JsonElement root)
    {
        var dto = new OfferAnalysisDto { OfferId = offerId };

        // 1. Try to get data from AI Agent format ("analyzed_offer" property)
        // OR fallback to the root level (Manual Submission format)
        var source = root.TryGetProperty("analyzed_offer", out var ao) ? ao : root;

        dto.Titre = source.GetStringOrDefault("titre") ?? "Poste non défini";
        dto.Entreprise = source.GetStringOrDefault("entreprise") ?? "Entreprise non renseignée";
        dto.TypeContrat = source.GetStringOrDefault("type_contrat");
        dto.Localisation = source.GetStringOrDefault("localisation");
        dto.DescriptionPoste = source.GetStringOrDefault("description_poste");
        dto.AnneesExperience = source.GetIntOrDefault("annees_experience");
        dto.NiveauEtudes = source.GetStringOrDefault("niveau_etudes");
        dto.CompetencesRequises = source.GetStringList("competences_requises");
        dto.CompetencesSouhaitees = source.GetStringList("competences_souhaitees");
        dto.KeywordsAts = source.GetStringList("keywords_ats");

        // 2. Try to get scoring from AI Agent format ("match_result" property)
        if (root.TryGetProperty("match_result", out var mr))
        {
            dto.ScoreMatching = mr.GetIntOrDefault("score_matching") ?? 0;
            dto.ScoreAts = mr.GetIntOrDefault("score_ats") ?? 0;
            dto.KeywordsPresents = mr.GetStringList("keywords_presents");
            dto.KeywordsManquants = mr.GetStringList("keywords_manquants");
            dto.Recommandations = mr.GetStringList("recommandations");
            dto.CompetencesMatching = mr.GetStringList("competences_matching");
            dto.CompetencesManquantes = mr.GetStringList("competences_manquantes");
        }

        // 3. Errors pipeline
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
