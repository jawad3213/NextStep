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
        if (string.IsNullOrEmpty(userId)) return new List<SessionSummaryDto>();

        // Jointure directe pour garantir que l'on trouve les sessions 
        // quel que soit l'ID (Keycloak ou Interne) fourni dans le token.
        var query = from s in _db.SessionCoachings
                    join u in _db.Utilisateurs on s.IdUtilisateur equals u.Id
                    where u.KeycloakId == userId || u.Id.ToString() == userId
                    where s.Status != "pending"
                    orderby s.DateSession descending
                    select new SessionSummaryDto(
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
                    );

        return await query.ToListAsync();
    }

    public async Task<SessionDetailDto> GetSessionDetailAsync(string sessionId)
    {
        var session = await _db.SessionCoachings
            .FindAsync(Guid.Parse(sessionId));

        if (session == null) throw new KeyNotFoundException();

        // Le FeedbackDto doit être désérialisé en tenant compte du format snake_case de Python
        var options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var feedback = session.FeedbackJson != null
            ? JsonSerializer.Deserialize<FeedbackDto>(session.FeedbackJson, options)
            : null;

        return new SessionDetailDto(
            SessionId    : session.IdSession.ToString(),
            Mode         : session.Mode,
            Domain       : session.Domain,
            Level        : session.Level,
            GlobalScore  : feedback?.GlobalScore ?? session.ScoreEntretien,
            DateSession  : session.DateSession,
            Dimensions   : feedback?.Dimensions ?? [],
            Strengths    : feedback?.Strengths ?? [],
            Improvements : feedback?.Improvements ?? [],
            CoachingTips : feedback?.CoachingTips ?? [],
            QuestionEvaluations : feedback?.QuestionEvaluations ?? [],
            BestAnswer   : feedback?.BestAnswer,
            WorstAnswer  : feedback?.WorstAnswer
        );
    }

    public async Task<bool> DeleteSessionAsync(string sessionId, string userId)
    {
        if (!Guid.TryParse(sessionId, out var sessionGuid)) return false;

        var session = await _db.SessionCoachings.FindAsync(sessionGuid);
        if (session == null) return false;

        var internalUser = await _db.Utilisateurs
            .FirstOrDefaultAsync(u => u.KeycloakId == userId || u.Id.ToString() == userId);

        if (internalUser == null || session.IdUtilisateur != internalUser.Id)
        {
            return false;
        }

        _db.SessionCoachings.Remove(session);
        await _db.SaveChangesAsync();
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Offers page — sidebar
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<UserOfferSummaryDto>> GetUserOffersAsync(string userId)
    {
        // Resolve Keycloak sub → internal UUID
        var internalUser = await _db.Utilisateurs
            .FirstOrDefaultAsync(u => u.KeycloakId == userId || u.Id.ToString() == userId);

        if (internalUser == null) return [];

        // Get all candidatures for this user, with their associated analyzed offer
        var candidatureIds = await _db.Candidatures
            .Where(c => c.IdUtilisateur == internalUser.Id)
            .Select(c => c.IdOffre)
            .ToListAsync();

        if (candidatureIds.Count == 0) return [];

        // Raw SQL query against the agent tables (not EF-mapped write tables)
        var result = new List<UserOfferSummaryDto>();

        foreach (var offreId in candidatureIds)
        {
            var sql = $"""
                SELECT id, id_offre, titre_poste, entreprise, localisation, type_contrat,
                       competences_requises, annees_experience, date_analyse
                FROM public.offre_analysee
                WHERE id_offre = '{offreId}'
                LIMIT 1
            """;

            var rows = await _db.Database
                .SqlQueryRaw<OffreAnalyseeRaw>(sql)
                .ToListAsync();

            if (rows.Count == 0) continue;
            var r = rows[0];

            // Parse JSONB arrays
            List<string> skills = [];
            try
            {
                if (!string.IsNullOrEmpty(r.CompetencesRequises))
                    skills = System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.CompetencesRequises) ?? [];
            }
            catch { /* ignore parse errors */ }

            // Get matching score from resultat_matching
            int? matchScore = null;
            var matchSql = $"""
                SELECT score_global FROM public.resultat_matching
                WHERE id_offre = '{offreId}' AND id_utilisateur = '{internalUser.Id}'
                LIMIT 1
            """;
            var matchRows = await _db.Database
                .SqlQueryRaw<MatchScoreRaw>(matchSql)
                .ToListAsync();
            if (matchRows.Count > 0) matchScore = matchRows[0].ScoreGlobal;

            result.Add(new UserOfferSummaryDto(
                OfferId         : r.IdOffre.ToString(),
                JobTitle        : r.TitrePoste ?? "Unknown Position",
                Company         : r.Entreprise ?? "Unknown Company",
                Location        : r.Localisation,
                ContractType    : r.TypeContrat,
                MatchingScore   : matchScore,
                YearsExperience : r.AnneesExperience,
                RequiredSkills  : skills.Take(6).ToList(),
                DateAnalysed    : r.DateAnalyse
            ));
        }

        return result;
    }
}

// ── Raw query projection types ──
file class OffreAnalyseeRaw
{
    public Guid Id { get; set; }
    public Guid IdOffre { get; set; }
    public string? TitrePoste { get; set; }
    public string? Entreprise { get; set; }
    public string? Localisation { get; set; }
    public string? TypeContrat { get; set; }
    public string? CompetencesRequises { get; set; }
    public int? AnneesExperience { get; set; }
    public DateTime DateAnalyse { get; set; }
}

file class MatchScoreRaw
{
    public int? ScoreGlobal { get; set; }
}