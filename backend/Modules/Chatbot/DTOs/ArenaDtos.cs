using System.Text.Json.Serialization;

namespace NextStep.Modules.Chatbot.DTOs;

// ─────────────────────────────────────────────
// Shared sub-objects
// ─────────────────────────────────────────────

/// <summary>
/// Config Arena : vient du stepper Angular (mode arena uniquement).
/// En mode offer ce champ est null — les données viennent de la DB.
/// </summary>
public record ArenaConfigDto(
    string Domain,
    string Level,
    int DurationMinutes,
    string Language,
    List<string> FocusAreas
);

/// <summary>
/// Un tour de conversation (question IA ou réponse user).
/// Utilisé pour passer l'historique à Python à chaque appel.
/// </summary>
public record MessageTurnDto(
    string Role,
    string Content
);

// ─────────────────────────────────────────────
// Tab 1 — Questions
// ─────────────────────────────────────────────

public record QuestionsRequest(
    string? Mode = null,
    string? UserId = null,
    string? OfferId = null,
    ArenaConfigDto? ArenaConfig = null
);

public record QuestionItemDto(
    string Id,
    string Question,
    string Type,
    string Source,
    bool CompanySpecific,
    string? Tip
);

public record QuestionsResponse(
    List<QuestionItemDto> Questions,
    string? SessionId
);

// ─────────────────────────────────────────────
// Tab 1 — Free Chat
// ─────────────────────────────────────────────

public record FreeChatRequest(
    string UserInput,
    string ThreadId,
    List<MessageTurnDto> History,
    string? Mode = null,
    string? UserId = null,
    string? OfferId = null,
    ArenaConfigDto? ArenaConfig = null,
    string? ChatType = null,
    SalaryContextDto? SalaryContext = null
);

public record FreeChatResponse(
    string Response,
    string ThreadId
);

// ─────────────────────────────────────────────
// Tab 2 — Interview : Start
// ─────────────────────────────────────────────

public record StartSessionRequest(
    string? SessionId = null,
    string? Mode = null,
    string? UserId = null,
    string? OfferId = null,
    string? CandidatureId = null,
    ArenaConfigDto? ArenaConfig = null,
    List<QuestionItemDto>? Questions = null
);

public record StartSessionResponse(
    string SessionId,
    string OpeningMessage
);

// ─────────────────────────────────────────────
// Tab 2 — Interview : Send Message
// ─────────────────────────────────────────────

public record SendMessageRequest(
    string SessionId,
    string UserInput,
    List<MessageTurnDto> History,
    string? Mode = null,
    string? UserId = null,
    string? OfferId = null,
    ArenaConfigDto? ArenaConfig = null
);

public record SendMessageResponse(
    string AiResponse,
    string SessionId
);

// ─────────────────────────────────────────────
// Tab 2 — Interview : End + Evaluation
// ─────────────────────────────────────────────

public record EndSessionRequest(
    string SessionId,
    List<MessageTurnDto> History,
    string? Mode = null,
    string? UserId = null,
    string? OfferId = null,
    ArenaConfigDto? ArenaConfig = null
);

public record DimensionScoreDto(
    string Name,
    int Score,
    string Comment
);

public record QuestionEvaluationDto(
    string Question,
    string UserAnswer,
    int Score,
    string Correction
);

public record FeedbackDto(
    int GlobalScore,
    List<DimensionScoreDto> Dimensions,
    List<QuestionEvaluationDto> QuestionEvaluations,
    List<string> Strengths,
    List<string> Improvements,
    string BestAnswer,
    string WorstAnswer,
    List<string> CoachingTips
);

public record EndSessionResponse(
    string SessionId,
    int Score,
    FeedbackDto Feedback
);

// ─────────────────────────────────────────────
// Tab 3 — Salary Coach
// ─────────────────────────────────────────────

public record SalaryRequest(
    string? Mode = null,
    string? UserId = null,
    string? OfferId = null,
    ArenaConfigDto? ArenaConfig = null
);

public record NegotiationStepDto(
    int Step,
    string Action,
    string Phrase,
    string Why
);

public record SalaryResponse(
    int RangeMin,
    int RangeMax,
    string Currency,
    int YourTarget,
    string ConfidenceLevel,
    List<string> MarketSources,
    List<NegotiationStepDto> NegotiationScript
);

// ─────────────────────────────────────────────
// Sessions History

public record SessionDetailDto(
    string SessionId,
    string Mode,
    string? Domain,
    string? Level,
    int? GlobalScore,
    DateTime DateSession,
    List<DimensionScoreDto> Dimensions,
    List<string> Strengths,
    List<string> Improvements,
    List<string> CoachingTips,
    List<QuestionEvaluationDto> QuestionEvaluations,
    string? BestAnswer = null,
    string? WorstAnswer = null,
    string? JobTitle = null,
    string? Company = null,
    string Language = "en",
    int DurationMinutes = 20
);

public record SessionSummaryDto(
    string SessionId,
    string Mode,
    string Status,
    string Language,
    int DurationMinutes,
    string? Domain,
    string? Level,
    int? ScoreEntretien,
    DateTime DateSession,
    DateTime? CompletedAt,
    string? JobTitle = null,
    string? Company = null
);


// ─────────────────────────────────────────────
// Salary Coach Interactive
// ─────────────────────────────────────────────

public record SalaryContextDto(
    int RangeMin,
    int RangeMax,
    string Currency,
    int YourTarget
);

public record SalaryCoachRequest(
    string UserInput,
    string ThreadId,
    SalaryContextDto SalaryContext,
    List<MessageTurnDto> History,
    string? Mode = null,
    string? UserId = null,
    string? OfferId = null,
    string? ChatType = null,
    ArenaConfigDto? ArenaConfig = null
);

public record SalaryCoachResponse(
    string Status,
    string Response
);

// ─────────────────────────────────────────────
// User Offers (sidebar page)
// ─────────────────────────────────────────────

/// <summary>
/// Légère carte affichée sur la page Offers du sidebar.
/// Alimente le bouton "Prepare for Interview" (mode offer du chatbot).
/// </summary>
public record UserOfferSummaryDto(
    string OfferId,
    string JobTitle,
    string Company,
    string? Location,
    string? ContractType,
    int? MatchingScore,
    int? YearsExperience,
    List<string> RequiredSkills,
    DateTime DateAnalysed
);
