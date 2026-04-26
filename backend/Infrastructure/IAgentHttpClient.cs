using System.Text.Json;

namespace NextStep.Infrastructure.Http;

public interface IAgentHttpClient
{
    Task<JsonDocument> RunPipelineAsync(
        string rawText,
        string userId,
        int templateId,
        CancellationToken cancellationToken = default);

    Task<JsonDocument> AnalyzeOfferAsync(
        string rawText,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> IsHealthyAsync(
        CancellationToken cancellationToken = default);

    Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativeUrl,
        TRequest payload,
        CancellationToken cancellationToken = default);
}