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
            dto.AnneesExperience = ao.GetIntOrDefault("annees_experience");
            dto.NiveauEtudes = ao.GetStringOrDefault("niveau_etudes");
            dto.CompetencesRequises = ao.GetStringList("competences_requises");
            dto.CompetencesSouhaitees = ao.GetStringList("competences_souhaitees");
            dto.KeywordsAts = ao.GetStringList("keywords_ats");
        }

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

        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            dto.Erreurs = [.. errors.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)];
        }

        return dto;
    }
}

public static class JsonElementExtensions
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
