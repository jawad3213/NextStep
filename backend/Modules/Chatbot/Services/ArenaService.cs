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
        if (request.OfferId != null && !string.IsNullOrEmpty(request.UserId))
        {
            try
            {
                var internalUser = await _db.Utilisateurs
                    .FirstOrDefaultAsync(u => u.KeycloakId.ToLower() == request.UserId.ToLower() || u.Id.ToString().ToLower() == request.UserId.ToLower());

                if (internalUser != null)
                {
                    var offerGuid = Guid.Parse(request.OfferId);
                    
                    // 1. S'assurer que l'offre existe dans la table public.offre pour satisfaire la FK de candidature
                    var offerExists = await CheckOfferExistsAsync(offerGuid);

                    if (!offerExists)
                    {
                        // Récupérer les détails depuis public.offre_analysee pour créer l'entrée correspondante
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

                        var escTitle = title.Replace("'", "''");
                        var escCompany = company.Replace("'", "''");
                        var escLoc = location.Replace("'", "''");

                        await _db.Database.ExecuteSqlRawAsync($"""
                            INSERT INTO public.offre (id_offre, date_scraping, entreprise, localisation, url_source, description_brute, titre_poste)
                            VALUES ('{offerGuid}', NOW(), '{escCompany}', '{escLoc}', '', 'Auto-created from chat session', '{escTitle}')
                        """);
                    }

                    // 2. S'assurer qu'une candidature existe pour cet utilisateur et cette offre
                    var candExists = await _db.Candidatures
                        .AnyAsync(c => c.IdUtilisateur == internalUser.Id && c.IdOffre == offerGuid);

                    if (!candExists)
                    {
                        var newCand = new NextStep.Modules.Candidature.Models.Candidature
                        {
                            IdCandidature = Guid.NewGuid(),
                            IdUtilisateur = internalUser.Id,
                            IdOffre = offerGuid,
                            Statut = "ENTRETIEN",
                            DateCreation = DateTime.UtcNow
                        };
                        _db.Candidatures.Add(newCand);
                        await _db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] StartSessionAsync auto-create candidature failed: {ex.Message}");
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

        // 1. Fetch sessions
        var query = from s in _db.SessionCoachings
                    join u in _db.Utilisateurs on s.IdUtilisateur equals u.Id
                    where u.KeycloakId.ToLower() == userId.ToLower() || u.Id.ToString().ToLower() == userId.ToLower()
                    where s.Status != "pending"
                    orderby s.DateSession descending
                    select s;

        var dbSessions = await query.ToListAsync();
        var result = new List<SessionSummaryDto>();

        foreach (var s in dbSessions)
        {
            string? jobTitle = null;
            string? company = null;

            if (s.Mode == "offer")
            {
                var candId = s.IdCandidature;
                if (!candId.HasValue)
                {
                    // Fallback : Trouver la première candidature de l'utilisateur
                    var candRow = await _db.Candidatures
                        .FirstOrDefaultAsync(c => c.IdUtilisateur == s.IdUtilisateur);
                    if (candRow != null)
                    {
                        candId = candRow.IdCandidature;
                    }
                }

                if (candId.HasValue)
                {
                    var cand = await _db.Candidatures.FindAsync(candId.Value);
                    if (cand != null)
                    {
                        try
                        {
                            var row = await QueryOffreAnalyseeAsync(cand.IdOffre);
                            if (row != null)
                            {
                                jobTitle = row.TitrePoste;
                                company = row.Entreprise;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"DEBUG GET_SESSIONS EXCEPTION: {ex.Message} - {ex.StackTrace}");
                        }
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

        string? jobTitle = null;
        string? company = null;

        if (session.Mode == "offer")
        {
            var candId = session.IdCandidature;
            if (!candId.HasValue)
            {
                var candRow = await _db.Candidatures
                    .FirstOrDefaultAsync(c => c.IdUtilisateur == session.IdUtilisateur);
                if (candRow != null)
                {
                    candId = candRow.IdCandidature;
                }
            }

            if (candId.HasValue)
            {
                var cand = await _db.Candidatures.FindAsync(candId.Value);
                if (cand != null)
                {
                    try
                    {
                        var row = await QueryOffreAnalyseeAsync(cand.IdOffre);
                        if (row != null)
                        {
                            jobTitle = row.TitrePoste;
                            company = row.Entreprise;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"DEBUG GET_SESSION_DETAIL EXCEPTION: {ex.Message} - {ex.StackTrace}");
                    }
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
            Company      : company
        );
    }

    public async Task<bool> DeleteSessionAsync(string sessionId, string userId)
    {
        if (!Guid.TryParse(sessionId, out var sessionGuid)) return false;

        var session = await _db.SessionCoachings.FindAsync(sessionGuid);
        if (session == null) return false;

        var internalUser = await _db.Utilisateurs
            .FirstOrDefaultAsync(u => u.KeycloakId.ToLower() == userId.ToLower() || u.Id.ToString().ToLower() == userId.ToLower());

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
            .FirstOrDefaultAsync(u => u.KeycloakId.ToLower() == userId.ToLower() || u.Id.ToString().ToLower() == userId.ToLower());

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
            var r = await QueryOffreAnalyseeAsync(offreId);
            if (r == null) continue;

            // Parse JSONB arrays
            List<string> skills = [];
            try
            {
                if (!string.IsNullOrEmpty(r.CompetencesRequises))
                    skills = System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.CompetencesRequises) ?? [];
            }
            catch { /* ignore parse errors */ }

            // Get matching score from resultat_matching
            int? matchScore = await QueryMatchScoreAsync(offreId, internalUser.Id);

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

    private async Task<bool> CheckOfferExistsAsync(Guid offerId)
    {
        var conn = _db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();
        try
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT 1 FROM public.offre WHERE id_offre = @idOffre LIMIT 1";
                var p = cmd.CreateParameter();
                p.ParameterName = "@idOffre";
                p.Value = offerId;
                cmd.Parameters.Add(p);
                var val = await cmd.ExecuteScalarAsync();
                return val != null && val != DBNull.Value;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] CheckOfferExistsAsync Exception: {ex.Message}");
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }
        return false;
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
                    FROM public.offre_analysee 
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
            Console.WriteLine($"[DEBUG] QueryOffreAnalyseeAsync Exception: {ex.Message}");
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
                    SELECT score_global FROM public.resultat_matching 
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
            Console.WriteLine($"[DEBUG] QueryMatchScoreAsync Exception: {ex.Message}");
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

public class MatchScoreRaw
{
    public int? ScoreGlobal { get; set; }
}