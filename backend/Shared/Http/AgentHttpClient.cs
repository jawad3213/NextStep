using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace NextStep.Shared.Http;

public class AgentHttpClient : IAgentHttpClient
{
    private readonly HttpClient _client;
    private readonly ILogger<AgentHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public AgentHttpClient(
        HttpClient client,
        IOptions<AgentPythonOptions> options,
        ILogger<AgentHttpClient> logger)
    {
        _client = client;
        _client.BaseAddress = new Uri(options.Value.Url);
        _client.Timeout = TimeSpan.FromSeconds(120); // pipeline IA peut prendre du temps
        _logger = logger;
    }

    /// <summary>
    /// Appelle POST /run-pipeline sur les agents Python.
    /// Lance le pipeline complet (6 agents) et retourne le résultat.
    /// </summary>
    public async Task<JsonDocument> RunPipelineAsync(
        string rawText,
        string userId,
        int templateId,
        CancellationToken ct = default)
    {
        var payload = new
        {
            raw_text = rawText,
            user_id = userId,
            template_id = templateId,
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("AgentHttpClient — POST /run-pipeline pour user_id={UserId}", userId);

        var response = await _client.PostAsync("/run-pipeline", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("AgentHttpClient — Erreur pipeline : {Status} — {Error}",
                response.StatusCode, error);
            throw new HttpRequestException(
                $"Erreur pipeline IA : {response.StatusCode} — {error}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(responseJson);
    }

    /// <summary>
    /// Appelle POST /analyze-offer — Agent 1 uniquement (analyse LLM).
    /// </summary>
    public async Task<JsonDocument> AnalyzeOfferAsync(
        string rawText,
        string userId,
        CancellationToken ct = default)
    {
        var payload = new
        {
            raw_text = rawText,
            user_id = userId,
            template_id = 1,
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/analyze-offer", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(responseJson);
    }

    /// <summary>Health check des agents Python.</summary>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetAsync("/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generic POST method used by EmailService and others.
    /// </summary>
    public async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken ct = default)
    {
        var response = await _client.PostAsJsonAsync(url, data, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken: ct)
               ?? throw new InvalidOperationException($"Failed to parse response from {url}.");
    }
}
