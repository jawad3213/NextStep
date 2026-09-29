using NextStep.Modules.Coaching.Application.Dtos;

namespace NextStep.Modules.Coaching.Application.Services;

/// <summary>
/// Business service contract for the Chatbot module.
/// The Controller depends only on this interface — never on the concrete implementation.
/// </summary>
public interface IArenaService
{
    // ── Tab 1 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates training questions.
    /// Arena: from ArenaConfig (Angular stepper).
    /// Offer: Python reads offre_analysee + intel_entreprise + resultat_matching from the DB.
    /// </summary>
    Task<QuestionsResponse> GenerateQuestionsAsync(QuestionsRequest request);

    /// <summary>
    /// Free-form preparation chat (Questions tab).
    /// Persists each exchange in chat_message (chat_type = "questions").
    /// </summary>
    Task<FreeChatResponse> FreeChatAsync(FreeChatRequest request);

    // ── Tab 2 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts a simulated interview session.
    /// Creates a SessionCoaching record in DB (status = "active").
    /// Returns the SessionId + the AI recruiter's opening message.
    /// </summary>
    Task<StartSessionResponse> StartSessionAsync(StartSessionRequest request);

    /// <summary>
    /// Sends a user message during the interview.
    /// Persists 2 ChatMessage records in DB (user + ai, chat_type = "interview").
    /// </summary>
    Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request);

    /// <summary>
    /// Ends the session and triggers AI evaluation.
    /// Updates SessionCoaching: score, feedback_json, status = "completed", completed_at.
    /// </summary>
    Task<EndSessionResponse> EndSessionAsync(EndSessionRequest request);

    // ── Tab 3 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Salary analysis + negotiation script.
    /// Arena: Tavily data from ArenaConfig.
    /// Offer: Tavily data + intel_entreprise salary range (agents 3).
    /// </summary>
    Task<SalaryResponse> GetSalaryAsync(SalaryRequest request);

    /// <summary>
    /// Interactive chat for salary negotiation. Reuses free-chat logic.
    /// </summary>
    Task<SalaryCoachResponse> SalaryCoachAsync(SalaryCoachRequest request);

    // ── History ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the session history for a user.
    /// </summary>
    Task<List<SessionSummaryDto>> GetSessionsAsync(string userId);

    /// <summary>
    /// Returns the details (full feedback) of a completed session.
    /// </summary>
    /// <summary>The user's own session; another user's session is reported as not found.</summary>
    Task<SessionDetailDto> GetSessionDetailAsync(string sessionId, string userId);

    /// <summary>
    /// Deletes an interview session.
    /// </summary>
    Task<bool> DeleteSessionAsync(string sessionId, string userId);

    /// <summary>
    /// Returns the user's analyzed offers (offre_analysee),
    /// from their applications — used by the Offers sidebar page.
    /// </summary>
    Task<List<UserOfferSummaryDto>> GetUserOffersAsync(string userId);
}