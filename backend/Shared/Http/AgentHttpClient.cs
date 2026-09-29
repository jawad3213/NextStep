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

    // Bounded, lightweight retry (no extra NuGet dependency). Retries connection
    // failures and 5xx replies only: never a timeout, because the agent may still be
    // working and a retry would run the same job twice (LLM cost + duplicate DB writes).
    private const int MaxAttempts = 3;

    // Per-call time limits (the HttpClient itself has no global timeout).
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);
    // The full pipeline (analysis, CV optimisation, company intel, email) can take several
    // minutes; PipelineRunnerService cancels the whole run after 10 minutes.
    private static readonly TimeSpan PipelineTimeout = TimeSpan.FromMinutes(9);

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
        // Time limits are applied per call (see DefaultTimeout / PipelineTimeout).
        _client.Timeout = Timeout.InfiniteTimeSpan;
        _logger = logger;
    }

    /// <summary>
    /// Calls POST /run-pipeline on the Python agents.
    /// Runs the full pipeline (6 agents) and returns the result.
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
        // Merge base data with resume data if present
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

        // Never retried: each run writes results to the database and costs LLM calls.
        using var response = await SendWithRetryAsync(
            token => _client.PostAsync("/offer/run-pipeline", JsonPayload(payload), token),
            "run-pipeline", ct, PipelineTimeout, maxAttempts: 1);

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
            token => _client.PostAsync("/offer/analyze-offer", JsonPayload(payload), token),
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
            token => _client.PostAsync("/offer/match", JsonPayload(payload), token),
            "match", ct);

        return await ReadJsonAsync(response, ct);
    }

    /// <summary>Health check des agents Python.</summary>
    /// <summary>
    /// Generic POST method used by EmailService and others.
    /// </summary>
    public async Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest data, CancellationToken ct = default)
    {
        using var response = await SendWithRetryAsync(
            token => _client.PostAsJsonAsync(url, data, JsonOptions, token),
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
            token => _client.PostAsJsonAsync(url, data, JsonOptions, token),
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
            token => _client.PostAsync(url, form, token),
            url, ct);

        if (!response.IsSuccessStatusCode)
            ThrowUpstreamError(url, response, ct);

        return await response.Content.ReadAsStringAsync(ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static HttpContent JsonPayload(object payload) =>
        new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> request,
        string operation,
        CancellationToken ct,
        TimeSpan? timeout = null,
        int maxAttempts = MaxAttempts)
    {
        var limit = timeout ?? DefaultTimeout;
        HttpRequestException? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(limit);

            try
            {
                var response = await request(timeoutCts.Token);

                // Retry only on 5xx: the agent answered, so the failed attempt is over.
                if ((int)response.StatusCode < 500 || attempt == maxAttempts)
                    return response;

                var status = response.StatusCode;
                response.Dispose();
                lastException = new HttpRequestException(
                    $"Agent responded {status} on {operation} (attempt {attempt}).");
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timed out: the agent may still be processing. Retrying would start the same
                // work a second time, so stop here.
                _logger.LogWarning("Agent call {Operation} timed out after {Seconds}s (not retried).",
                    operation, limit.TotalSeconds);
                throw new HttpRequestException(
                    $"Agent call {operation} timed out after {limit.TotalSeconds:0}s.");
            }

            if (attempt < maxAttempts)
            {
                _logger.LogWarning("Agent call {Operation} failed (attempt {Attempt}/{Max}), retrying…",
                    operation, attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
            }
        }

        throw lastException
            ?? new HttpRequestException($"Agent call {operation} failed after {maxAttempts} attempts.");
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
