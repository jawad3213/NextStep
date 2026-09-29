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
/// Interview coach (Arena). The Python agents only compute (questions, interviewer replies,
/// evaluation); this service owns the coaching data: it saves the sessions and questions
/// in the "coaching" schema around each agent call and reuses questions already generated.
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
    /// Questions for an offer or an Arena configuration. Questions already generated for the
    /// same application (offer mode) or the same domain/level/language (Arena) are reused;
    /// otherwise the agents generate them and they are saved with a new "pending" session.
    /// </summary>
    public async Task<QuestionsResponse> GenerateQuestionsAsync(QuestionsRequest request)
    {
        var userId = await ResolveUserIdAsync(request.UserId);
        var candidatureId = await FindCandidatureIdAsync(userId, request.OfferId);

        var reusable = await FindReusableQuestionsAsync(request, userId, candidatureId);
        if (reusable is not null)
            return reusable;

        var response = await _agentClient.PostQuestionsAsync(request);
        if (userId is null || response?.Questions is null || response.Questions.Count == 0)
            return response!;

        var config = request.ArenaConfig;
        var session = NewSession(userId.Value, candidatureId, request.Mode, config, status: "pending");
        var saved = response.Questions.Select((q, index) => new QuestionEntrainement
        {
            IdSession = session.IdSession,
            TexteQuestion = q.Question,
            TypeQuestion = q.Type,
            Source = q.Source,
            CompanySpecific = q.CompanySpecific,
            ConseilReponse = q.Tip,
            Ordre = index,
        }).ToList();

        _db.SessionCoachings.Add(session);
        _db.QuestionEntrainements.AddRange(saved);
        await _db.SaveChangesAsync();

        return new QuestionsResponse(saved.Select(ToQuestionDto).ToList(), session.IdSession.ToString());
    }

    /// <summary>
    /// Free chat (Questions tab).
    /// After the AI response: persists the 2 messages (user + ai) in chat_message.
    /// </summary>
    public async Task<FreeChatResponse> FreeChatAsync(FreeChatRequest request)
    {
        // Python handles persisting messages in chat_message
        return await _agentClient.PostFreeChatAsync(request);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 2 — Interview
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the session in DB BEFORE calling Python.
    /// Python needs the session_id to persist its own data (question_entrainement).
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

                    // 1. An offer unknown to the Applications module gets a placeholder text
                    if (!await _applications.OfferExistsAsync(offerGuid))
                        placeholderRawText = "Auto-created from chat session";

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
                // Best-effort: auto-creation of the application must not
                // block the session, but the failure must remain traceable.
                _logger.LogWarning(ex, "ArenaService — auto-creation of application ignored for the session.");
            }
        }

        // The session is created here, then the agents write the opening message for it.
        var userId = await ResolveUserIdAsync(request.UserId);
        var sessionId = await CreateInterviewSessionAsync(request, userId);
        var response = await _agentClient.PostStartInterviewAsync(request with { SessionId = sessionId.ToString() });
        return response with { SessionId = sessionId.ToString() };
    }

    /// <summary>
    /// Sends the user message to Python, persists user + AI response in DB.
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
            _logger.LogError(ex, "ArenaService — send message failed for session {SessionId}.", request.SessionId);
            throw new OperationFailedException("Error sending message. Please try again.", ex);
        }
    }

    /// <summary>
    /// Calls Python for evaluation, then updates SessionCoaching in DB.
    /// </summary>
    public async Task<EndSessionResponse> EndSessionAsync(EndSessionRequest request)
    {
        // The agents evaluate the interview; the score and feedback are saved here.
        EndSessionResponse response;
        try
        {
            response = await _agentClient.PostEndInterviewAsync(request);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            _logger.LogError(ex, "ArenaService — end session failed for {SessionId}.", request.SessionId);
            throw new OperationFailedException("Error ending session. Please try again.", ex);
        }

        await CompleteSessionAsync(request, response);
        return response;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 3 — Salary
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Forwards to Python — no specific persistence for the salary coach.
    /// Python does the Tavily search + reads intel_entreprise if offer mode.
    /// </summary>
    public async Task<SalaryResponse> GetSalaryAsync(SalaryRequest request)
    {
        return await _agentClient.PostSalaryAsync(request);
    }

    /// <summary>
    /// Interactive chat for salary negotiation.
    /// Redirects to Python's free-chat logic since the LLM adapts to context via the supplied history.
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
    // Session persistence (the coaching data belongs to this module)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Feedback is stored in the agents' snake_case format (read by GetSessionDetailAsync).</summary>
    private static readonly JsonSerializerOptions FeedbackJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private async Task<Guid?> ResolveUserIdAsync(string? userRef) =>
        string.IsNullOrEmpty(userRef) ? null : await _profile.FindUserIdAsync(userRef);

    private async Task<Guid?> FindCandidatureIdAsync(Guid? userId, string? offerId)
    {
        if (userId is null || !Guid.TryParse(offerId, out var offerGuid)) return null;
        var application = await _applications.FindApplicationForOfferAsync(userId.Value, offerGuid);
        return application?.CandidatureId;
    }

    private static SessionCoaching NewSession(Guid userId, Guid? candidatureId, string? mode, ArenaConfigDto? config, string status) => new()
    {
        IdSession = Guid.NewGuid(),
        IdUtilisateur = userId,
        IdCandidature = candidatureId,
        Mode = mode ?? (candidatureId.HasValue ? "offer" : "arena"),
        Language = config?.Language ?? "en",
        DurationMinutes = config?.DurationMinutes ?? 20,
        Domain = config?.Domain,
        Level = config?.Level,
        FocusAreas = config?.FocusAreas is { } areas ? JsonSerializer.Serialize(areas) : null,
        Status = status,
        DateSession = DateTime.UtcNow,
    };

    private static QuestionItemDto ToQuestionDto(QuestionEntrainement q) =>
        new(q.IdQuestion.ToString(), q.TexteQuestion, q.TypeQuestion, q.Source, q.CompanySpecific, q.ConseilReponse);

    /// <summary>Questions already generated for this application / Arena configuration, or null.</summary>
    private async Task<QuestionsResponse?> FindReusableQuestionsAsync(QuestionsRequest request, Guid? userId, Guid? candidatureId)
    {
        if (userId is null) return null;

        IQueryable<SessionCoaching> sessions;
        if (request.Mode == "offer" && candidatureId.HasValue)
        {
            sessions = _db.SessionCoachings.Where(s => s.IdCandidature == candidatureId.Value);
        }
        else if (request.Mode == "arena" && request.ArenaConfig is { } config)
        {
            sessions = _db.SessionCoachings.Where(s =>
                s.IdUtilisateur == userId.Value && s.Mode == "arena"
                && s.Domain == config.Domain && s.Level == config.Level && s.Language == config.Language);
        }
        else
        {
            return null;
        }

        var sessionId = await sessions
            .Where(s => _db.QuestionEntrainements.Any(q => q.IdSession == s.IdSession))
            .OrderByDescending(s => s.DateSession)
            .Select(s => (Guid?)s.IdSession)
            .FirstOrDefaultAsync();
        if (sessionId is null) return null;

        var questions = await _db.QuestionEntrainements
            .Where(q => q.IdSession == sessionId.Value)
            .OrderBy(q => q.Ordre)
            .ToListAsync();
        _logger.LogInformation("ArenaService — {Count} questions reused from session {SessionId}.", questions.Count, sessionId);
        return new QuestionsResponse(questions.Select(ToQuestionDto).ToList(), sessionId.Value.ToString());
    }

    /// <summary>
    /// Creates the "started" interview session. Reuses the given id when it is free or already
    /// the user's own session; an unknown user gets an unsaved id (the interview still works).
    /// </summary>
    private async Task<Guid> CreateInterviewSessionAsync(StartSessionRequest request, Guid? userId)
    {
        var requested = Guid.TryParse(request.SessionId, out var id) ? id : (Guid?)null;
        if (userId is null) return requested ?? Guid.NewGuid();

        if (requested is { } existingId && await _db.SessionCoachings.FindAsync(existingId) is { } existing)
        {
            if (existing.IdUtilisateur == userId.Value) return existingId;
            requested = null; // someone else's id: never reuse it
        }

        var candidatureId = Guid.TryParse(request.CandidatureId, out var cid)
            ? cid
            : await FindCandidatureIdAsync(userId, request.OfferId);
        var session = NewSession(userId.Value, candidatureId, request.Mode, request.ArenaConfig, status: "started");
        if (requested is { } free) session.IdSession = free;

        _db.SessionCoachings.Add(session);
        await _db.SaveChangesAsync();
        return session.IdSession;
    }

    /// <summary>Saves score and feedback on the user's own session.</summary>
    private async Task CompleteSessionAsync(EndSessionRequest request, EndSessionResponse response)
    {
        var userId = await ResolveUserIdAsync(request.UserId);
        if (!Guid.TryParse(request.SessionId, out var sessionId) || userId is null) return;

        var session = await _db.SessionCoachings.FindAsync(sessionId);
        if (session is null || session.IdUtilisateur != userId.Value)
        {
            _logger.LogWarning("ArenaService — session {SessionId} not found for this user; evaluation not saved.", request.SessionId);
            return;
        }

        session.Status = "completed";
        session.ScoreEntretien = response.Score;
        session.CompletedAt = DateTime.UtcNow;
        session.FeedbackJson = JsonSerializer.Serialize(response.Feedback, FeedbackJsonOptions);
        await _db.SaveChangesAsync();
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
                    var offer = await OfferSummaryAsync(s.IdUtilisateur, cand.OfferId.Value);
                    jobTitle = offer?.Title;
                    company = offer?.Company;
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

    public async Task<SessionDetailDto> GetSessionDetailAsync(string sessionId, string userId)
    {
        // Another user's session is reported as not found (its existence is not revealed).
        var internalUserId = await ResolveUserIdAsync(userId);
        var session = Guid.TryParse(sessionId, out var id) && internalUserId.HasValue
            ? await _db.SessionCoachings.FirstOrDefaultAsync(s => s.IdSession == id && s.IdUtilisateur == internalUserId.Value)
            : null;

        if (session == null) throw new NotFoundException("Session not found.");

        // The FeedbackDto must be deserialized considering the snake_case format from Python
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
                var offer = await OfferSummaryAsync(session.IdUtilisateur, cand.OfferId.Value);
                jobTitle = offer?.Title;
                company = offer?.Company;
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

        // Analysis of each offer, read through the Applications module's contract
        var result = new List<UserOfferSummaryDto>();

        foreach (var offreId in candidatureIds)
        {
            var offer = await OfferSummaryAsync(internalUserId.Value, offreId);
            if (offer == null) continue;

            result.Add(new UserOfferSummaryDto(
                OfferId         : offer.OfferId.ToString(),
                JobTitle        : string.IsNullOrWhiteSpace(offer.Title) ? "Unknown Position" : offer.Title,
                Company         : offer.Company ?? "Unknown Company",
                Location        : offer.Location,
                ContractType    : offer.ContractType,
                MatchingScore   : offer.MatchingScore,
                YearsExperience : offer.YearsExperience,
                RequiredSkills  : offer.RequiredSkills.Take(6).ToList(),
                DateAnalysed    : offer.AnalysedAt ?? DateTime.MinValue
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

    /// <summary>The offer's analysis (Applications module), or null when missing or unreadable.</summary>
    private async Task<OfferSummary?> OfferSummaryAsync(Guid userId, Guid offerId)
    {
        try
        {
            return await _applications.GetOfferSummaryAsync(userId, offerId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ArenaService — analyse de l'offre {OfferId} indisponible.", offerId);
            return null;
        }
    }
}

