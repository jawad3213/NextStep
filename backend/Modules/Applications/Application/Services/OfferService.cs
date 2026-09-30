using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Mappings;
using NextStep.Modules.Applications.Domain;
using NextStep.Modules.Applications.Infrastructure.Repositories;
using NextStep.Modules.Applications.Infrastructure.Persistence;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Events;

namespace NextStep.Modules.Applications.Application.Services;

public interface IOfferService
{
    Task<OffreEmploi> SaveOfferAsync(string rawText, string userId, CancellationToken ct = default);
    Task<OfferAnalysisDto?> GetAnalysisAsync(Guid userId, Guid offerId, CancellationToken ct = default);
    Task<List<OfferHistoryItemDto>> GetHistoryAsync(Guid userId, CancellationToken ct = default);
    Task<int> DeleteOffersAsync(Guid userId, List<Guid> offerIds, CancellationToken ct = default);
    Task SavePipelineResultAsync(Guid offerId, JsonDocument pipelineResult, Guid userId, CancellationToken ct = default);
    Task<OffreEmploi?> GetOfferWithAnalysisAsync(Guid userId, Guid offerId, CancellationToken ct = default);

    /// <summary>
    /// Existence + ownership check without loading the offer payload. Used by the
    /// fire-and-forget endpoints, which must still reject unknown or foreign offers
    /// before acknowledging the request.
    /// </summary>
    Task<bool> OfferBelongsToUserAsync(Guid userId, Guid offerId, CancellationToken ct = default);
}

public class OfferService(
    IOfferRepository repository,
    ApplicationsDbContext db,
    ICvContentSanitizer cvSanitizer,
    IEventPublisher events,
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
        logger.LogInformation("OfferService — Offer saved {OfferId}", offre.Id);
        return offre;
    }

    public async Task SavePipelineResultAsync(Guid offerId, JsonDocument pipelineResult, Guid userId, CancellationToken ct = default)
    {
        await EnsureOfferOwnedAsync(userId, offerId, ct);

        var root = pipelineResult.RootElement;
        var fullJson = root.GetRawText();
        var offer = await db.OffresEmploi.FirstOrDefaultAsync(o => o.Id == offerId, ct);

        if (offer is null)
        {
            throw new InvalidOperationException($"Offer {offerId} not found before pipeline persistence.");
        }

        // A generation run re-saves the whole payload, but the agent answer does not know which
        // analysis run produced it. Without carrying the marker over, a CV generation would erase
        // the run id, and a client still waiting on a background analysis could then accept a
        // result it had already seen.
        if (!root.TryGetProperty("analysis_run_id", out _) && !string.IsNullOrEmpty(offer.AnalyseJson))
        {
            var previousRunId = (JsonNode.Parse(offer.AnalyseJson) as JsonObject)?["analysis_run_id"]?.ToString();

            if (!string.IsNullOrEmpty(previousRunId))
            {
                var merged = JsonNode.Parse(fullJson)!.AsObject();
                merged["analysis_run_id"] = previousRunId;
                fullJson = merged.ToJsonString();
            }
        }

        // Save the latest pipeline payload first so the frontend can reload the
        // fresh analysis/CV output even if draft persistence fails later on.
        offer.AnalyseJson = fullJson;
        await db.SaveChangesAsync(ct);

        var cvDataJson = root.TryGetProperty("cv_data", out var cd)
            ? cvSanitizer.Sanitize(cd)
            : null;

        // Analysis-only runs should only persist the offer analysis.
        // We create candidature/documents once the generation phase returns cv_data.
        if (cvDataJson != null)
        {
            try
            {
                var candidature = await db.Candidatures
                    .FirstOrDefaultAsync(c =>
                        c.IdOffre == offerId &&
                        c.IdUtilisateur == userId,
                        ct);

                if (candidature == null)
                {
                    candidature = new NextStep.Modules.Applications.Domain.Candidature
                    {
                        IdUtilisateur = userId,
                        IdOffre = offerId,
                        Offre = offer,
                        Statut = "EN_ATTENTE",
                        DateCreation = DateTime.UtcNow
                    };
                    db.Candidatures.Add(candidature);
                    await db.SaveChangesAsync(ct);
                }

                var existingDocument = await db.DocumentsGeneres
                    .FirstOrDefaultAsync(d => d.IdCandidature == candidature.IdCandidature, ct);

                if (existingDocument != null)
                {
                    existingDocument.CvContenuIaJson = cvDataJson;
                    existingDocument.Version += 1;
                    existingDocument.DateGeneration = DateTime.UtcNow;
                }
                else
                {
                    var documentGenere = new DocumentGenere
                    {
                        IdCandidature = candidature.IdCandidature,
                        CvContenuIaJson = cvDataJson,
                        Version = 1,
                        DateGeneration = DateTime.UtcNow
                    };
                    db.DocumentsGeneres.Add(documentGenere);
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "OfferService — CV draft persistence failed for offer {OfferId}. analyse_json was saved and will still be returned to the frontend.",
                    offerId);
            }
        }

        logger.LogInformation("OfferService — Pipeline results saved for offer {OfferId}", offerId);
    }

    public async Task<OfferAnalysisDto?> GetAnalysisAsync(Guid userId, Guid offerId, CancellationToken ct = default)
    {
        await EnsureOfferOwnedAsync(userId, offerId, ct, "Analysis not found.");

        var offre = await repository.GetByIdAsync(offerId, ct);
        if (offre is null) return null;
        if (string.IsNullOrEmpty(offre.AnalyseJson)) return null;

        var doc = JsonDocument.Parse(offre.AnalyseJson);
        return OfferAnalysisMapper.ToAnalysisDto(offre.Id, doc.RootElement, offre.TexteBrut);
    }


    public async Task<List<OfferHistoryItemDto>> GetHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        var offers = await db.OffresEmploi
            .Where(o => o.UtilisateurId == userId)
            .OrderByDescending(o => o.DateCreation)
            .ToListAsync(ct);

        var offerIds = offers.Select(o => o.Id).ToList();
        var generatedOfferIds = await (
            from c in db.Candidatures
            join d in db.DocumentsGeneres on c.IdCandidature equals d.IdCandidature
            where c.IdOffre.HasValue && offerIds.Contains(c.IdOffre.Value)
            select c.IdOffre!.Value
        ).Distinct().ToListAsync(ct);
        var generatedSet = generatedOfferIds.ToHashSet();

        var list = new List<OfferHistoryItemDto>(offers.Count);
        foreach (var offer in offers)
        {
            string title = "Offer";
            string company = "";
            string location = "";
            int? score = null;
            var status = "non_traitee";
            var currentStep = 1;

            if (!string.IsNullOrWhiteSpace(offer.AnalyseJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(offer.AnalyseJson);
                    var dto = OfferAnalysisMapper.ToAnalysisDto(offer.Id, doc.RootElement, offer.TexteBrut);
                    title = string.IsNullOrWhiteSpace(dto.Titre) ? title : dto.Titre;
                    company = dto.Entreprise ?? "";
                    location = dto.Localisation ?? "";
                    score = dto.ScoreMatching > 0 ? dto.ScoreMatching : null;
                    status = "analysee";
                    currentStep = 2;
                }
                catch (JsonException parseEx)
                {
                    logger.LogWarning(parseEx,
                        "OfferService — Corrupted AnalyseJson for offer {OfferId}, degraded processing.",
                        offer.Id);
                    status = "analysee";
                    currentStep = 2;
                }
            }

            if (generatedSet.Contains(offer.Id))
            {
                status = "cv_genere";
                currentStep = 5;
            }

            list.Add(new OfferHistoryItemDto
            {
                OfferId = offer.Id,
                Titre = title,
                Entreprise = company,
                Localisation = location,
                ScoreMatching = score,
                Status = status,
                CurrentStep = currentStep,
                DateCreation = offer.DateCreation,
            });
        }

        return list;
    }

    public async Task<int> DeleteOffersAsync(Guid userId, List<Guid> offerIds, CancellationToken ct = default)
    {
        if (offerIds.Count == 0) return 0;

        var uniqueOfferIds = offerIds
            .Distinct()
            .ToList();

        var offers = await db.OffresEmploi
            .Where(o => o.UtilisateurId == userId && uniqueOfferIds.Contains(o.Id))
            .ToListAsync(ct);

        if (offers.Count == 0) return 0;

        var ownedOfferIds = offers
            .Select(o => o.Id)
            .ToList();

        var candidatureIds = await db.Candidatures
            .Where(c => c.IdOffre.HasValue && ownedOfferIds.Contains(c.IdOffre.Value))
            .Select(c => c.IdCandidature)
            .ToListAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Notes, status history and documents of these applications cascade in the database.
        await db.Candidatures
            .Where(c => candidatureIds.Contains(c.IdCandidature))
            .ExecuteDeleteAsync(ct);

        db.OffresEmploi.RemoveRange(offers);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        // Other modules (email drafts, coaching sessions) clean up their own data.
        if (candidatureIds.Count > 0)
            await events.PublishAsync(new CandidaturesDeleted(userId, candidatureIds), ct);

        logger.LogInformation(
            "OfferService - deleted {OfferCount} offers and cleaned {CandidatureCount} linked candidatures for user {UserId}",
            offers.Count,
            candidatureIds.Count,
            userId);

        return offers.Count;
    }

    public async Task<OffreEmploi?> GetOfferWithAnalysisAsync(Guid userId, Guid offerId, CancellationToken ct = default)
    {
        await EnsureOfferOwnedAsync(userId, offerId, ct);
        return await repository.GetByIdWithAnalysisAsync(offerId, ct);
    }

    public Task<bool> OfferBelongsToUserAsync(Guid userId, Guid offerId, CancellationToken ct = default)
        => db.OffresEmploi.AnyAsync(o => o.Id == offerId && o.UtilisateurId == userId, ct);

    private async Task EnsureOfferOwnedAsync(Guid userId, Guid offerId, CancellationToken ct, string notFoundMessage = "Offer not found.")
    {
        var exists = await db.OffresEmploi.AnyAsync(o => o.Id == offerId && o.UtilisateurId == userId, ct);
        if (!exists) throw new NotFoundException(notFoundMessage);
    }
}
