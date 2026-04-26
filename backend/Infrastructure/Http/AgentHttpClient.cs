using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NextStep.Infrastructure.Http;

public class AgentHttpClient : IAgentHttpClient
{
    private readonly HttpClient _client;
    private readonly ILogger<AgentHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public AgentHttpClient(
        HttpClient client,
        IOptions<AgentPythonOptions> options,
        ILogger<AgentHttpClient> logger)
    {
        _client = client;
        _logger = logger;

        var baseUrl = string.IsNullOrWhiteSpace(options.Value.BaseUrl)
            ? "http://localhost:8000"
            : options.Value.BaseUrl;

        _client.BaseAddress = new Uri(baseUrl);
        _client.Timeout = TimeSpan.FromSeconds(120);
    }

    public async Task<JsonDocument> RunPipelineAsync(
        string rawText,
        string userId,
        int templateId,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            raw_text = rawText,
            user_id = userId,
            template_id = templateId
        };

        return await PostJsonDocumentAsync(
            "/run-pipeline",
            payload,
            cancellationToken);
    }

    public async Task<JsonDocument> AnalyzeOfferAsync(
        string rawText,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            raw_text = rawText,
            user_id = userId,
            template_id = 1
        };

        return await PostJsonDocumentAsync(
            "/analyze-offer",
            payload,
            cancellationToken);
    }

    public async Task<bool> IsHealthyAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativeUrl,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(relativeUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(
                "AgentHttpClient error: {RelativeUrl} — {StatusCode} — {Error}",
                relativeUrl,
                response.StatusCode,
                error);

            throw new HttpRequestException(
                $"Agent request failed: {response.StatusCode} — {error}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        var result = JsonSerializer.Deserialize<TResponse>(
            responseJson,
            JsonOptions);

        if (result is null)
            throw new InvalidOperationException("Agent returned empty or invalid response.");

        return result;
    }

    private async Task<JsonDocument> PostJsonDocumentAsync(
        string relativeUrl,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("AgentHttpClient — POST {RelativeUrl}", relativeUrl);

        var response = await _client.PostAsync(relativeUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(
                "AgentHttpClient pipeline error: {RelativeUrl} — {StatusCode} — {Error}",
                relativeUrl,
                response.StatusCode,
                error);

            throw new HttpRequestException(
                $"Agent pipeline failed: {response.StatusCode} — {error}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(responseJson);
    }
}