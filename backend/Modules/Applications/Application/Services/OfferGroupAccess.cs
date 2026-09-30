using NextStep.Shared.Realtime;

namespace NextStep.Modules.Applications.Application.Services;

/// <summary>Applications owns the offers, so it decides who may observe an offer's events.</summary>
public class OfferGroupAccess(IOfferService offerService) : IOfferGroupAccess
{
    public Task<bool> CanAccessOfferGroupAsync(Guid userId, Guid offerId, CancellationToken ct = default)
        => offerService.OfferBelongsToUserAsync(userId, offerId, ct);
}
