using NextStep.Modules.Coaching.Infrastructure.Agents;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NextStep.Modules.Coaching.Application.Dtos;
using NextStep.Modules.Coaching.Application.Services;
using NextStep.Modules.Coaching.Domain;
using NextStep.Modules.Coaching.Infrastructure.Persistence;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Coaching.Application.Services;

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
    private readonly CoachingDbContext _db;
    private readonly IApplicationsApi _applications;
    private readonly IProfileApi _profile;
    private readonly ILogger<ArenaService> _logger;

    public ArenaService(
        IAgentHttpClient agentClient,
        CoachingDbContext db,
        IApplicationsApi applications,
        IProfileApi profile,
        ILogger<ArenaService> logger)
    {
        _agentClient = agentClient;
        _db = db;
        _applications = applications;
        _profile = profile;
        _logger = logger;
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
        if (request.OfferId != null && !string.IsNullOrEmpty(request.UserId))
        {
            try
            {
                var internalUserId = await _profile.FindUserIdAsync(request.UserId);

                if (internalUserId.HasValue)
                {
                    var offerGuid = Guid.Parse(request.OfferId);
                    string? placeholderRawText = null;

                    // 1. The offer may only exist in the agents' analysis: build its text from there
                    if (!await _applications.OfferExistsAsync(offerGuid))
                    {
                        // Récupérer les détails depuis agents.offre_analysee pour créer l'entrée correspondante
                        var offerDetails = await QueryOffreAnalyseeAsync(offerGuid);

                        string title = "Offre de Stage";
                        string company = "ALTEN Maroc";
                        string location = "Maroc";
                        if (offerDetails != null)
                        {
                            title = offerDetails.TitrePoste ?? title;
                            company = offerDetails.Entreprise ?? company;
                            location = offerDetails.Localisation ?? location;
                        }

                        placeholderRawText =
                            $"Auto-created from chat session{Environment.NewLine}" +
                            $"Title: {title}{Environment.NewLine}" +
                            $"Company: {company}{Environment.NewLine}" +
                            $"Location: {location}";
                    }

                    // 2. Make sure the user has an application for this offer (owned by Applications)
                    await _applications.EnsureApplicationForOfferAsync(
                        internalUserId.Value,
                        offerGuid,
                        placeholderRawText ?? "Auto-created from chat session",
                        "ENTRETIEN");
                }
            }
            catch (Exception ex)
            {
                // Best-effort : l'auto-création de la candidature ne doit pas
                // bloquer la session, mais l'échec doit rester traçable.
                _logger.LogWarning(ex, "ArenaService — auto-création de candidature ignorée pour la session.");
            }
        }

        // Python se charge de créer la session en DB et de retourner le session_id
        return await _agentClient.PostStartInterviewAsync(request);
    }

    /// <summary>
    /// Envoie le message user à Python, persiste user + réponse IA en DB.
    /// </summary>
    public async Task<SendMessageResponse> SendMessageAsync(SendMessageRequest request)
    {
        // Python se charge de persister les messages dans chat_message
        try
        {
            return await _agentClient.PostSendMessageAsync(request);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            _logger.LogError(ex, "ArenaService — envoi du message échoué pour la session {SessionId}.", request.SessionId);
            throw new OperationFailedException("Erreur lors de l'envoi du message. Veuillez réessayer.", ex);
        }
    }

    /// <summary>
    /// Appelle Python pour l'évaluation, puis met à jour la SessionCoaching en DB.
    /// </summary>
    public async Task<EndSessionResponse> EndSessionAsync(EndSessionRequest request)
    {
        // Python se charge d'évaluer et de mettre à jour la SessionCoaching
        try
        {
            return await _agentClient.PostEndInterviewAsync(request);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            _logger.LogError(ex, "ArenaService — fin de session échouée pour {SessionId}.", request.SessionId);
            throw new OperationFailedException("Erreur lors de la fin de session. Veuillez réessayer.", ex);
        }
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
            ArenaConfig: request.ArenaConfig,
            ChatType: request.ChatType ?? "salary",
            SalaryContext: request.SalaryContext
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

        var internalUserId = await _profile.FindUserIdAsync(userId);
        if (!internalUserId.HasValue) return new List<SessionSummaryDto>();

        // 1. Fetch sessions
        var dbSessions = await _db.SessionCoachings
            .Where(s => s.IdUtilisateur == internalUserId.Value && s.Status != "pending")
            .OrderByDescending(s => s.DateSession)
            .ToListAsync();
        var result = new List<SessionSummaryDto>();

        foreach (var s in dbSessions)
        {
            string? jobTitle = null;
            string? company = null;

            if (s.Mode == "offer")
            {
                var cand = await ResolveSessionApplicationAsync(s);
                if (cand?.OfferId is not null)
                {
                    try
                    {
                        var row = await QueryOffreAnalyseeAsync(cand.OfferId.Value);
                        if (row != null)
                        {
                            jobTitle = row.TitrePoste;
                            company = row.Entreprise;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "ArenaService — enrichissement offre ignoré pour {CandidatureId}.", cand.CandidatureId);
                    }
                }
            }

            result.Add(new SessionSummaryDto(
                s.IdSession.ToString(),
                s.Mode,
                s.Status,
                s.Language,
                s.DurationMinutes,
                s.Domain,
                s.Level,
                s.ScoreEntretien,
                s.DateSession,
                s.CompletedAt,
                jobTitle,
                company
            ));
        }

        return result;
    }

    public async Task<SessionDetailDto> GetSessionDetailAsync(string sessionId)
    {
        var session = await _db.SessionCoachings
            .FindAsync(Guid.Parse(sessionId));

        if (session == null) throw new NotFoundException("Session introuvable.");

        // Le FeedbackDto doit être désérialisé en tenant compte du format snake_case de Python
        var options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var feedback = session.FeedbackJson != null
            ? JsonSerializer.Deserialize<FeedbackDto>(session.FeedbackJson, options)
            : null;

        string? jobTitle = null;
        string? company = null;

        if (session.Mode == "offer")
        {
            var cand = await ResolveSessionApplicationAsync(session);
            if (cand?.OfferId is not null)
            {
                try
                {
                    var row = await QueryOffreAnalyseeAsync(cand.OfferId.Value);
                    if (row != null)
                    {
                        jobTitle = row.TitrePoste;
                        company = row.Entreprise;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "ArenaService — détail offre ignoré pour le détail de session.");
                }
            }
        }

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
            QuestionEvaluations: feedback?.QuestionEvaluations ?? [],
            BestAnswer   : feedback?.BestAnswer,
            WorstAnswer  : feedback?.WorstAnswer,
            JobTitle     : jobTitle,
            Company      : company,
            Language     : session.Language,
            DurationMinutes : session.DurationMinutes
        );
    }

    public async Task<bool> DeleteSessionAsync(string sessionId, string userId)
    {
        if (!Guid.TryParse(sessionId, out var sessionGuid)) return false;

        var session = await _db.SessionCoachings.FindAsync(sessionGuid);
        if (session == null) return false;

        var internalUserId = await _profile.FindUserIdAsync(userId);

        if (internalUserId == null || session.IdUtilisateur != internalUserId.Value)
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
        var internalUserId = await _profile.FindUserIdAsync(userId);

        if (internalUserId == null) return [];

        // Offers the user applied to (owned by Applications), enriched with the agents' analysis
        var candidatureIds = await _applications.ListAppliedOfferIdsAsync(internalUserId.Value);

        if (candidatureIds.Count == 0) return [];

        // Raw SQL query against the agent tables (not EF-mapped write tables)
        var result = new List<UserOfferSummaryDto>();

        foreach (var offreId in candidatureIds)
        {
            var r = await QueryOffreAnalyseeAsync(offreId);
            if (r == null) continue;

            // Parse JSONB arrays
            List<string> skills = [];
            try
            {
                if (!string.IsNullOrEmpty(r.CompetencesRequises))
                    skills = System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.CompetencesRequises) ?? [];
            }
            catch (JsonException parseEx)
            {
                _logger.LogWarning(parseEx, "ArenaService — compétences illisibles pour l'offre {OfferId}.", offreId);
            }

            // Get matching score from resultat_matching
            int? matchScore = await QueryMatchScoreAsync(offreId, internalUserId.Value);

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

    /// <summary>The session's application, or the user's first one for sessions started without it.</summary>
    private async Task<ApplicationSnapshot?> ResolveSessionApplicationAsync(SessionCoaching session)
    {
        return session.IdCandidature.HasValue
            ? await _applications.GetApplicationAsync(session.IdCandidature.Value)
            : await _applications.FindFirstApplicationAsync(session.IdUtilisateur);
    }

    private async Task<OffreAnalyseeRaw?> QueryOffreAnalyseeAsync(Guid idOffre)
    {
        var conn = _db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT id, id_offre, titre_poste, entreprise, localisation, type_contrat, competences_requises::text, annees_experience, date_analyse 
                    FROM agents.offre_analysee 
                    WHERE id_offre = @idOffre 
                    LIMIT 1
                """;
                var p = cmd.CreateParameter();
                p.ParameterName = "@idOffre";
                p.Value = idOffre;
                cmd.Parameters.Add(p);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new OffreAnalyseeRaw
                        {
                            Id = reader.IsDBNull(0) ? Guid.Empty : reader.GetGuid(0),
                            IdOffre = reader.IsDBNull(1) ? Guid.Empty : reader.GetGuid(1),
                            TitrePoste = reader.IsDBNull(2) ? null : reader.GetString(2),
                            Entreprise = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Localisation = reader.IsDBNull(4) ? null : reader.GetString(4),
                            TypeContrat = reader.IsDBNull(5) ? null : reader.GetString(5),
                            CompetencesRequises = reader.IsDBNull(6) ? null : reader.GetString(6),
                            AnneesExperience = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                            DateAnalyse = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8)
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ArenaService — requête offre analysée échouée pour {OfferId}.", idOffre);
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
        return null;
    }

    private async Task<int?> QueryMatchScoreAsync(Guid idOffre, Guid idUtilisateur)
    {
        var conn = _db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT score_global FROM agents.resultat_matching 
                    WHERE id_offre = @idOffre AND id_utilisateur = @idUtilisateur 
                    LIMIT 1
                """;
                
                var p1 = cmd.CreateParameter();
                p1.ParameterName = "@idOffre";
                p1.Value = idOffre;
                cmd.Parameters.Add(p1);

                var p2 = cmd.CreateParameter();
                p2.ParameterName = "@idUtilisateur";
                p2.Value = idUtilisateur;
                cmd.Parameters.Add(p2);

                var val = await cmd.ExecuteScalarAsync();
                if (val != null && val != DBNull.Value)
                {
                    return Convert.ToInt32(val);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ArenaService — récupération du score matching échouée pour {OfferId}.", idOffre);
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
        return null;
    }
}

// ── Raw query projection types ──
public class OffreAnalyseeRaw
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

