using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Chatbot.DTOs;
using NextStep.Modules.Chatbot.Interfaces;
using NextStep.Modules.Chatbot.Models;
using NextStep.data;

namespace NextStep.Modules.Chatbot.Services;

/// <summary>
/// Service métier du module Chatbot.
/// Responsabilités :
///   1. Valider et enrichir les requêtes
///   2. Persister en DB (session_coaching, chat_message)
///   3. Déléguer l'IA à Python via IAgentHttpClient
///   4. Mapper les réponses Python vers les DTOs .NET
/// </summary>
public class ArenaService : IArenaService
{
    private readonly IAgentHttpClient _agentClient;
    private readonly AppDbContext _db; // ton DbContext partagé NextStep

    public ArenaService(IAgentHttpClient agentClient, AppDbContext db)
    {
        _agentClient = agentClient;
        _db = db;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 1 — Questions
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transmet la requête à Python tel quel.
    /// Python se charge de lire la DB (mode offer) ou d'utiliser ArenaConfig (mode arena).
    /// Pas de persistance ici — les questions sont affichées mais pas encore "jouées".
    /// </summary>
    public async Task<QuestionsResponse> GenerateQuestionsAsync(QuestionsRequest request)
    {
        return await _agentClient.PostQuestionsAsync(request);
    }

    /// <summary>
    /// Chat libre (tab Questions).
    /// Après la réponse IA : on persiste les 2 messages (user + ai) dans chat_message.
    /// </summary>
    public async Task<FreeChatResponse> FreeChatAsync(FreeChatRequest request)
    {
        // Python se charge de persister les messages dans chat_message
        return await _agentClient.PostFreeChatAsync(request);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 2 — Interview
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crée la session en DB AVANT d'appeler Python.
    /// Python a besoin du session_id pour persister ses propres données (question_entrainement).
    /// </summary>
    public async Task<StartSessionResponse> StartSessionAsync(StartSessionRequest request)
    {
        // Python se charge de créer la session en DB et de retourner le session_id
        return await _agentClient.PostStartInterviewAsync(request);
    }

    /// <summary>
    /// Envoie le message user à Python, persiste user + réponse IA en DB.
    /// </summary>
    public async Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request)
    {
        // Python se charge de persister les messages dans chat_message
        return await _agentClient.PostSendMessageAsync(request);
    }

    /// <summary>
    /// Appelle Python pour l'évaluation, puis met à jour la SessionCoaching en DB.
    /// </summary>
    public async Task<EndSessionResponse> EndSessionAsync(EndSessionRequest request)
    {
        // Python se charge d'évaluer et de mettre à jour la SessionCoaching
        return await _agentClient.PostEndInterviewAsync(request);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 3 — Salary
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transmet à Python — pas de persistance spécifique pour le salary coach.
    /// Python fait la recherche Tavily + lit intel_entreprise si mode offer.
    /// </summary>
    public async Task<SalaryResponse> GetSalaryAsync(SalaryRequest request)
    {
        return await _agentClient.PostSalaryAsync(request);
    }

    /// <summary>
    /// Chat interactif pour la négociation salariale.
    /// Redirige vers la logique free-chat de Python car le LLM s'adapte au contexte via l'historique fourni.
    /// </summary>
    public async Task<SalaryCoachResponse> SalaryCoachAsync(SalaryCoachRequest request)
    {
        var freeChatReq = new FreeChatRequest(
            UserInput: request.UserInput,
            ThreadId: request.ThreadId,
            History: request.History,
            Mode: request.Mode ?? "arena",
            UserId: request.UserId ?? "",
            OfferId: request.OfferId,
            ArenaConfig: null
        );

        var response = await _agentClient.PostFreeChatAsync(freeChatReq);

        return new SalaryCoachResponse(
            Status: "ok",
            Response: response.Response
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Historique
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<SessionSummaryDto>> GetSessionsAsync(string userId)
    {
        var userGuid = Guid.Parse(userId);

        var sessions = await _db.SessionCoachings
            .Where(s => s.IdUtilisateur == userGuid)
            .OrderByDescending(s => s.DateSession)
            .Select(s => new SessionSummaryDto(
                s.IdSession.ToString(),
                s.Mode,
                s.Status,
                s.Language,
                s.DurationMinutes,
                s.Domain,
                s.Level,
                s.ScoreEntretien,
                s.DateSession,
                s.CompletedAt
            ))
            .ToListAsync();

        return sessions;
    }

    public async Task<FeedbackDto?> GetSessionDetailsAsync(string sessionId)
    {
        var sessionGuid = Guid.Parse(sessionId);
        var session = await _db.SessionCoachings
            .FirstOrDefaultAsync(s => s.IdSession == sessionGuid);

        if (session == null || string.IsNullOrEmpty(session.FeedbackJson))
            return null;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<FeedbackDto>(session.FeedbackJson, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ArenaService] Error deserializing FeedbackJson for session {sessionId}: {ex.Message}");
            return null;
        }
    }
}