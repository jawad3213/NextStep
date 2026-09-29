using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Mappings;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;

namespace NextStep.Modules.Applications.Application.Services;

public interface ISkillGapService
{
    /// <summary>
    /// Compares a pasted offer with the user's stored profile (analyse offer → match) without
    /// saving anything. Used by the Skill Gap page.
    /// </summary>
    Task<SkillGapResultDto> AnalyzeAsync(Guid userId, string offerText, CancellationToken ct = default);
}

public class SkillGapService(
    IAgentHttpClient agents,
    IProfileApi profile,
    ILogger<SkillGapService> logger) : ISkillGapService
{
    public async Task<SkillGapResultDto> AnalyzeAsync(Guid userId, string offerText, CancellationToken ct = default)
    {
        try
        {
            var userIdStr = userId.ToString();

            using var analyzeDoc = await agents.AnalyzeOfferAsync(offerText, userIdStr, ct);
            var analyzedOffer = analyzeDoc.RootElement.GetProperty("analyzed_offer");

            using var matchDoc = await agents.MatchProfileAsync(userIdStr, analyzedOffer, ct);
            var candidate = await profile.GetCandidateProfileAsync(userId, ct);

            return SkillGapMapper.ToResult(analyzedOffer, matchDoc.RootElement, candidate, DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is not AppException and not OperationCanceledException)
        {
            logger.LogError(ex, "Skill-gap analysis failed for user {UserId}", userId);
            throw new OperationFailedException("Skill gap analysis failed. Please try again.", ex);
        }
    }
}
