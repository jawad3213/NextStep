using System.Text.Json;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;

namespace NextStep.Modules.Applications.Application.Services;

public interface IOfferAnalysisService
{
    /// <summary>
    /// Runs the three agents synchronously (analyse offer → retrieve profile → match),
    /// stores the combined result on the offer and returns the analysis.
    /// </summary>
    Task<OfferAnalysisDto> AnalyzeAsync(Guid userId, Guid offerId, CancellationToken ct = default);

    /// <summary>Starts CV generation for the offer in the background (progress goes through SignalR).</summary>
    void StartGenerationInBackground(Guid userId, Guid offerId, int templateId);
}

public class OfferAnalysisService(
    IOfferService offerService,
    IAgentHttpClient agents,
    IServiceScopeFactory scopeFactory,
    ILogger<OfferAnalysisService> logger) : IOfferAnalysisService
{
    public async Task<OfferAnalysisDto> AnalyzeAsync(Guid userId, Guid offerId, CancellationToken ct = default)
    {
        var offer = await offerService.GetOfferWithAnalysisAsync(userId, offerId, ct)
            ?? throw new NotFoundException("Offre introuvable.");

        try
        {
            var userIdStr = userId.ToString();

            // a) Agent 1: offer analysis
            var analyzeDoc = await agents.AnalyzeOfferAsync(offer.TexteBrut ?? "", userIdStr, ct);
            var analyzedOffer = analyzeDoc.RootElement.GetProperty("analyzed_offer");

            // b) Agents 2 & 3: profile retriever + skill-gap match
            var matchDoc = await agents.MatchProfileAsync(userIdStr, analyzedOffer, ct);

            // Merge the results into the stored pipeline format
            var combined = new Dictionary<string, object>
            {
                { "analyzed_offer", JsonSerializer.Deserialize<object>(analyzedOffer.GetRawText())! },
                { "skill_gap_analysis", JsonSerializer.Deserialize<object>(matchDoc.RootElement.GetRawText())! },
                { "match_result", JsonSerializer.Deserialize<object>(matchDoc.RootElement.GetRawText())! }
            };
            if (matchDoc.RootElement.TryGetProperty("profile_data", out var profileData))
                combined["profile_data"] = JsonSerializer.Deserialize<object>(profileData.GetRawText())!;

            using var pipelineResult = JsonDocument.Parse(JsonSerializer.Serialize(combined));
            await offerService.SavePipelineResultAsync(offerId, pipelineResult, userId, ct);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            logger.LogError(ex, "Synchronous analysis failed for offer {OfferId} / user {UserId}", offerId, userId);
            throw new OperationFailedException("L'analyse de l'offre a échoué. Veuillez réessayer.", ex);
        }

        return await offerService.GetAnalysisAsync(userId, offerId, ct)
            ?? throw new OperationFailedException("Impossible de récupérer l'analyse après enregistrement.");
    }

    public void StartGenerationInBackground(Guid userId, Guid offerId, int templateId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IPipelineRunnerService>();
                await runner.StartGenerationAsync(offerId, userId.ToString(), templateId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pipeline [GENERATION] task failed for offer {OfferId}", offerId);
            }
        });
    }
}
