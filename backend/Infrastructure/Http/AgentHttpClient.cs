using System.Net.Http.Json;

namespace backend.Infrastructure.Http;

public class AgentHttpClient : IAgentHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentHttpClient> _logger;

    public AgentHttpClient(HttpClient httpClient, ILogger<AgentHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativeUrl,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(relativeUrl, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Python service call failed. Url={Url}, Status={StatusCode}, Body={Body}",
                relativeUrl, (int)response.StatusCode, body);

            throw new HttpRequestException($"Python service call failed: {(int)response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);

        if (result is null)
            throw new InvalidOperationException("Python service returned an empty response.");

        return result;
    }
}