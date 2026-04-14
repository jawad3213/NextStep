namespace backend.Infrastructure.Http;

public interface IAgentHttpClient
{
    Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativeUrl,
        TRequest payload,
        CancellationToken cancellationToken = default);
}