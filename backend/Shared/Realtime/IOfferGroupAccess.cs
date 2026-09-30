namespace NextStep.Shared.Realtime;

/// <summary>
/// Authorisation check for the offer notification groups used by <see cref="PipelineHub"/>.
/// The hub lives in the shared kernel, so it must not reach into a module's internals:
/// the module that owns the offers implements this contract.
/// </summary>
public interface IOfferGroupAccess
{
    /// <summary>True when the user owns the offer and may therefore receive its events.</summary>
    Task<bool> CanAccessOfferGroupAsync(Guid userId, Guid offerId, CancellationToken ct = default);
}
