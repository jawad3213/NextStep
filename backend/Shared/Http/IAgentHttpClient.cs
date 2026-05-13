using System.Text.Json;

namespace NextStep.Shared.Http;

public interface IAgentHttpClient
{
    Task<JsonDocument> RunPipelineAsync(
        string rawText,
        string userId,
        int templateId,
        Guid offerId,
        bool onlyAnalysis = false,
        object? resumeData = null,
        CancellationToken ct = default);
    Task<JsonDocument> AnalyzeOfferAsync(string rawText, string userId, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
    Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken ct = default);
}
