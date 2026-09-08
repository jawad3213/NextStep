using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace NextStep.Shared.Http;

public class AgentHttpClient : IAgentHttpClient
{
    private readonly HttpClient _client;
    private readonly ILogger<AgentHttpClient> _logger;

    // Bounded, lightweight retry (no extra NuGet dependency). Only retries
    // transient failures: timeouts, connection resets, 5xx from the agent.
    private const int MaxAttempts = 3;

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
        // Pipeline IA (6 agents + LLM + scraping) peut dépasser 2 min, mais on
        // borne à 120 s afin de ne pas occuper un thread indéfiniment.
        _client.Timeout = TimeSpan.FromSeconds(120);
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
        Guid offerId,
        bool onlyAnalysis = false,
        object? resumeData = null,
        CancellationToken ct = default)
    {
        // On fusionne les données de base avec les données de reprise si présentes
        var payload = new Dictionary<string, object>
        {
            ["raw_text"] = rawText,
            ["user_id"] = userId,
            ["template_id"] = templateId,
            ["offer_id"] = offerId.ToString(),
            ["only_analysis"] = onlyAnalysis
        };

        if (resumeData != null)
        {
            var resumeJson = JsonSerializer.Serialize(resumeData, JsonOptions);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(resumeJson, JsonOptions);
            if (dict != null)
            {
                foreach (var kv in dict)
                {
                    payload[kv.Key] = kv.Value;
                }
            }
        }

        _logger.LogInformation("AgentHttpClient — POST /run-pipeline (onlyAnalysis={OnlyAnalysis}) for user_id={UserId}", onlyAnalysis, userId);

        using var response = await SendWithRetryAsync(
            () => _client.PostAsync("/offer/run-pipeline", JsonPayload(payload), ct),
            "run-pipeline", ct);

        return await ReadJsonAsync(response, ct);
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
            offer_id = Guid.NewGuid().ToString(),
        };

        using var response = await SendWithRetryAsync(
            () => _client.PostAsync("/offer/analyze-offer", JsonPayload(payload), ct),
            "analyze-offer", ct);

        return await ReadJsonAsync(response, ct);
    }

    /// <summary>
    /// Appelle POST /match — Agents 2-3 uniquement (Matching profil ↔ offre).
    /// </summary>
    public async Task<JsonDocument> MatchProfileAsync(
        string userId,
        JsonElement analyzedOffer,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object>
        {
            ["user_id"] = userId,
            ["analyzed_offer"] = JsonSerializer.Deserialize<object>(analyzedOffer.GetRawText(), JsonOptions)!
        };

        using var response = await SendWithRetryAsync(
            () => _client.PostAsync("/offer/match", JsonPayload(payload), ct),
            "match", ct);

        return await ReadJsonAsync(response, ct);
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
        using var response = await SendWithRetryAsync(
            () => _client.PostAsJsonAsync(url, data, JsonOptions, ct),
            url, ct);

        if (!response.IsSuccessStatusCode)
            ThrowUpstreamError(url, response, ct);

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken: ct)
               ?? throw new InvalidOperationException($"Failed to parse response from {url}.");
    }

    /// <summary>
    /// Generic POST that returns the raw JSON body as a <see cref="JsonDocument"/>,
    /// used for endpoints whose schema is owned by the Python service (e.g. resume).
    /// </summary>
    public async Task<JsonDocument> PostRawAsync<TRequest>(string url, TRequest data, CancellationToken ct = default)
    {
        using var response = await SendWithRetryAsync(
            () => _client.PostAsJsonAsync(url, data, JsonOptions, ct),
            url, ct);

        if (!response.IsSuccessStatusCode)
            ThrowUpstreamError(url, response, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// Multipart upload toward an agent endpoint (e.g. PDF resume parsing).
    /// </summary>
    public async Task<string> PostFileAsync(
        string url,
        byte[] fileBytes,
        string fileName,
        string contentType = "application/pdf",
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        using var response = await SendWithRetryAsync(
            () => _client.PostAsync(url, form, ct),
            url, ct);

        if (!response.IsSuccessStatusCode)
            ThrowUpstreamError(url, response, ct);

        return await response.Content.ReadAsStringAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static HttpContent JsonPayload(object payload) =>
        new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> request,
        string operation,
        CancellationToken ct)
    {
        HttpRequestException? lastException = null;
        HttpResponseMessage? transientResponse = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var response = await request();

                // Retry only on 5xx from the agent (transient upstream failure).
                if ((int)response.StatusCode < 500 || attempt == MaxAttempts)
                    return response;

                transientResponse = response;
                lastException = new HttpRequestException(
                    $"Agent responded {response.StatusCode} on {operation} (attempt {attempt}).");
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                lastException = new HttpRequestException($"Agent call {operation} timed out (attempt {attempt}).");
            }

            _logger.LogWarning("Agent call {Operation} failed (attempt {Attempt}/{Max}), retrying…",
                operation, attempt, MaxAttempts);

            transientResponse?.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
        }

        throw lastException
            ?? new HttpRequestException($"Agent call {operation} failed after {MaxAttempts} attempts.");
    }

    private async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
            ThrowUpstreamError(response.RequestMessage?.RequestUri?.PathAndQuery ?? "agent", response, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(body);
    }

    private void ThrowUpstreamError(string url, HttpResponseMessage response, CancellationToken ct)
    {
        var errorBody = string.Empty;
        try
        {
            errorBody = response.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
        }
        catch
        {
            // best-effort: read the raw body only, never throw here
        }

        _logger.LogError("AgentHttpClient — upstream error {Status} on {Url}: {Body}",
            response.StatusCode, url, errorBody);

        throw new HttpRequestException(
            $"Agent '{url}' returned {response.StatusCode}: {errorBody}");
    }
}
