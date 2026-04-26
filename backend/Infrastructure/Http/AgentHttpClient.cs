// ============================================================
// Infrastructure/Http/AgentHttpClient.cs
// Client HTTP vers les Agents Python FastAPI
// ============================================================
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NextStep.Modules.Offer.DTOs;

namespace NextStep.Infrastructure.Http;

public class AgentPythonOptions
{
    public string BaseUrl { get; set; } = "http://agents-python:8000";
}

public class AgentHttpClient
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
        _client.BaseAddress = new Uri(options.Value.BaseUrl);
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
}
