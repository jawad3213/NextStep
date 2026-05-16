using NextStep.Modules.Chatbot.DTOs;

namespace NextStep.Modules.Chatbot.Interfaces;

/// <summary>
/// Contrat service métier du module Chatbot.
/// Le Controller dépend uniquement de cette interface — jamais de l'implémentation concrète.
/// </summary>
public interface IArenaService
{
    // ── Tab 1 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Génère les questions d'entraînement.
    /// Arena : depuis ArenaConfig (stepper Angular).
    /// Offer : Python lit offre_analysee + intel_entreprise + resultat_matching depuis la DB.
    /// </summary>
    Task<QuestionsResponse> GenerateQuestionsAsync(QuestionsRequest request);

    /// <summary>
    /// Chat libre de préparation (tab Questions).
    /// Persiste chaque échange dans chat_message (chat_type = "questions").
    /// </summary>
    Task<FreeChatResponse> FreeChatAsync(FreeChatRequest request);

    // ── Tab 2 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Démarre une session d'interview simulée.
    /// Crée un enregistrement SessionCoaching en DB (status = "active").
    /// Retourne le SessionId + le message d'ouverture du recruteur IA.
    /// </summary>
    Task<StartSessionResponse> StartSessionAsync(StartSessionRequest request);

    /// <summary>
    /// Envoie un message utilisateur pendant l'interview.
    /// Persiste 2 ChatMessage en DB (user + ai, chat_type = "interview").
    /// </summary>
    Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request);

    /// <summary>
    /// Termine la session et déclenche l'évaluation IA.
    /// Met à jour SessionCoaching : score, feedback_json, status = "completed", completed_at.
    /// </summary>
    Task<EndSessionResponse> EndSessionAsync(EndSessionRequest request);

    // ── Tab 3 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Analyse salariale + script de négociation.
    /// Arena : données Tavily depuis ArenaConfig.
    /// Offer : données Tavily + fourchette intel_entreprise (agents 3).
    /// </summary>
    Task<SalaryResponse> GetSalaryAsync(SalaryRequest request);

    /// <summary>
    /// Interactive chat for salary negotiation. Reuses free-chat logic.
    /// </summary>
    Task<SalaryCoachResponse> SalaryCoachAsync(SalaryCoachRequest request);

    // ── Historique ─────────────────────────────────────────────────────────

    /// <summary>
    /// Retourne l'historique des sessions d'un utilisateur.
    /// </summary>
    Task<List<SessionSummaryDto>> GetSessionsAsync(string userId);

    /// <summary>
    /// Retourne les détails (feedback complet) d'une session terminée.
    /// </summary>
    Task<SessionDetailDto> GetSessionDetailAsync(string sessionId);

    /// <summary>
    /// Supprime une session d'interview.
    /// </summary>
    Task<bool> DeleteSessionAsync(string sessionId, string userId);
}