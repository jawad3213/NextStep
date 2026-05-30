using CandidatureEntity = NextStep.Modules.Candidature.Models.Candidature;
using NextStep.Modules.Email.Models;

namespace NextStep.Modules.Email.Services;

/// <summary>
/// Holds the result of LLM-based recruiter reply classification.
/// ResponseType is always one of the 7 canonical types or "REPONSE_RECUE" (fallback).
/// </summary>
public sealed record ClassificationResult(
    string ResponseType,
    double Confidence,
    string Summary,
    string RecommendedAction,
    bool   ShouldGenerateReplyDraft = false
);

public interface IResponseClassificationService
{
    /// <summary>
    /// Classifies the detected recruiter reply using the Python LLM agent.
    /// Never throws — returns a safe fallback (<see cref="ClassificationResult"/> with
    /// ResponseType = "REPONSE_RECUE") on any failure.
    /// </summary>
    Task<ClassificationResult> ClassifyAsync(
        CandidatureEntity candidature,
        EmailDraft?       draft,
        ReplyCheckResult  reply,
        CancellationToken ct = default);
}
