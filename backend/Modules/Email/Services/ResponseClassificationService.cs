using System.Text.Json;
using System.Text.Json.Serialization;
using CandidatureEntity = NextStep.Modules.Candidature.Models.Candidature;
using NextStep.Modules.Email.Models;
using NextStep.Shared.Http;

namespace NextStep.Modules.Email.Services;

/// <summary>
/// Calls the Python email agent's /email/classify-response endpoint to classify a recruiter reply.
/// Never throws — all exceptions are caught and a safe fallback result is returned.
/// </summary>
public class ResponseClassificationService : IResponseClassificationService
{
    // ── Valid LLM-returned response types ────────────────────────────────────────
    private static readonly HashSet<string> ValidResponseTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ENTRETIEN_PROPOSE",
            "INFORMATIONS_DEMANDEES",
            "ACCEPTE",
            "REFUSE",
            "REPONSE_AUTOMATIQUE",
            "REPONSE_GENERALE",
            "INCONNU",
        };

    // ── Safe fallback (returned when Python/LLM fails or returns invalid data) ──
    private static readonly ClassificationResult Fallback = new(
        ResponseType:             "REPONSE_RECUE",
        Confidence:               0.0,
        Summary:                  "Une réponse a été détectée, mais l'analyse automatique a échoué.",
        RecommendedAction:        "Consultez la réponse manuellement.",
        ShouldGenerateReplyDraft: false
    );

    private readonly IAgentHttpClient _agentHttpClient;
    private readonly ILogger<ResponseClassificationService> _logger;

    public ResponseClassificationService(
        IAgentHttpClient agentHttpClient,
        ILogger<ResponseClassificationService> logger)
    {
        _agentHttpClient = agentHttpClient;
        _logger          = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(
        CandidatureEntity candidature,
        EmailDraft?       draft,
        ReplyCheckResult  reply,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Extract job context from offer JSON if available ──────────────
            string? jobTitle    = null;
            string? companyName = null;

            if (candidature.Offre?.AnalyseJson is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(candidature.Offre.AnalyseJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("job_title",    out var jt)) jobTitle    = jt.GetString();
                    if (root.TryGetProperty("company_name", out var cn)) companyName = cn.GetString();
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx,
                        "ResponseClassificationService — could not parse AnalyseJson for candidature {CandidatureId}",
                        candidature.IdCandidature);
                }
            }

            // ── 2. Build Python request payload ──────────────────────────────────
            // Truncate draft body to 1500 chars — snippet context is enough for classification
            var bodySnippet = draft?.Body is { Length: > 0 } b
                ? b[..Math.Min(1500, b.Length)]
                : null;

            var payload = new
            {
                candidature_id         = candidature.IdCandidature.ToString(),
                job_title              = jobTitle,
                company_name           = companyName,
                previous_email_subject = draft?.Subject,
                previous_email_body    = bodySnippet,
                reply_from             = reply.ReplyFrom,
                reply_date_utc         = reply.ReplyDateUtc?.ToString("o"),
                reply_subject          = reply.ReplySubject,
                reply_snippet          = reply.Snippet ?? string.Empty,
                language               = "fr",
            };

            _logger.LogInformation(
                "ResponseClassificationService — calling /email/classify-response for candidature {CandidatureId}",
                candidature.IdCandidature);

            // ── 3. Call Python agent ──────────────────────────────────────────────
            var pyResult = await _agentHttpClient
                .PostAsync<object, PythonClassifyResponse>("/email/classify-response", payload, ct);

            // ── 4. Validate response_type ─────────────────────────────────────────
            if (pyResult is null || !ValidResponseTypes.Contains(pyResult.ResponseType ?? string.Empty))
            {
                _logger.LogWarning(
                    "ResponseClassificationService — invalid/missing response_type '{Type}' " +
                    "for candidature {CandidatureId}; using fallback",
                    pyResult?.ResponseType, candidature.IdCandidature);
                return Fallback;
            }

            // ── 5. Normalise confidence ───────────────────────────────────────────
            var confidence = NormalizeConfidence(pyResult.Confidence);

            _logger.LogInformation(
                "ResponseClassificationService — result: type={Type}, confidence={Confidence:F2} " +
                "for candidature {CandidatureId}",
                pyResult.ResponseType, confidence, candidature.IdCandidature);

            return new ClassificationResult(
                ResponseType:             pyResult.ResponseType!,
                Confidence:               confidence,
                Summary:                  pyResult.Summary          ?? Fallback.Summary,
                RecommendedAction:        pyResult.RecommendedAction ?? Fallback.RecommendedAction,
                ShouldGenerateReplyDraft: pyResult.ShouldGenerateReplyDraft
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ResponseClassificationService — classification failed for candidature {CandidatureId}; using fallback",
                candidature.IdCandidature);
            return Fallback;
        }
    }

    // ── Confidence normalisation ──────────────────────────────────────────────────

    /// <summary>
    /// Normalises a raw confidence value from the LLM:
    /// <list type="bullet">
    ///   <item>NaN / Infinity → 0</item>
    ///   <item>Value in (1, 100] → divide by 100 (LLM returned a percentage)</item>
    ///   <item>Clamp result to [0, 1]</item>
    /// </list>
    /// </summary>
    private static double NormalizeConfidence(double raw)
    {
        if (double.IsNaN(raw) || double.IsInfinity(raw)) return 0.0;
        if (raw > 1.0 && raw <= 100.0) raw /= 100.0;
        return Math.Clamp(raw, 0.0, 1.0);
    }

    // ── Internal DTO for Python response deserialization ─────────────────────────

    private sealed class PythonClassifyResponse
    {
        [JsonPropertyName("response_type")]
        public string? ResponseType { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("recommended_action")]
        public string? RecommendedAction { get; set; }

        [JsonPropertyName("should_generate_reply_draft")]
        public bool ShouldGenerateReplyDraft { get; set; }
    }
}
