using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Chatbot.DTOs;
using NextStep.Modules.Chatbot.Interfaces;

namespace NextStep.Modules.Chatbot.Controllers;

[ApiController]
[Route("api/arena")]
[Authorize] // JWT validé par .NET avant tout appel Python
public class ArenaController : ControllerBase
{
    private readonly IArenaService _arenaService;

    public ArenaController(IArenaService arenaService)
    {
        _arenaService = arenaService;
    }

    private string GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
               ?? User.FindFirst("sub")?.Value 
               ?? "";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Health
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>GET /api/arena/health</summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult HealthCheck()
        => Ok(new { status = "ok", module = "chatbot" });

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 1 — Questions
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/arena/questions
    /// Arena : ArenaConfig requis, OfferId null.
    /// Offer : OfferId requis, ArenaConfig null.
    /// </summary>
    [HttpPost("questions")]
    public async Task<IActionResult> GenerateQuestions([FromBody] QuestionsRequest request)
    {
        request = request with { UserId = GetUserId(), Mode = request.OfferId != null ? "offer" : "arena" };
        var result = await _arenaService.GenerateQuestionsAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/arena/chat
    /// Chat libre de préparation (tab Questions).
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> FreeChat([FromBody] FreeChatRequest request)
    {
        request = request with { UserId = GetUserId(), Mode = request.OfferId != null ? "offer" : "arena" };
        var result = await _arenaService.FreeChatAsync(request);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 2 — Interview
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/arena/session/start
    /// Crée la session en DB + retourne le message d'ouverture du recruteur IA.
    /// </summary>
    [HttpPost("session/start")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request)
    {
        request = request with { UserId = GetUserId(), Mode = request.OfferId != null ? "offer" : "arena" };
        var result = await _arenaService.StartSessionAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/arena/session/message
    /// Envoie un message et retourne la réponse du recruteur IA.
    /// </summary>
    [HttpPost("session/message")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            request = request with { UserId = GetUserId(), Mode = request.OfferId != null ? "offer" : "arena" };
            var result = await _arenaService.SendMessageAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ArenaController] SendMessage Error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/arena/session/end
    /// Termine l'interview et retourne l'évaluation complète (score + 5 dimensions).
    /// </summary>
    [HttpPost("session/end")]
    public async Task<IActionResult> EndSession([FromBody] EndSessionRequest request)
    {
        try
        {
            request = request with { UserId = GetUserId(), Mode = request.OfferId != null ? "offer" : "arena" };
            var result = await _arenaService.EndSessionAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ArenaController] EndSession Error: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab 3 — Salary
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/arena/salary
    /// Analyse salariale + script de négociation.
    /// </summary>
    [HttpPost("salary")]
    public async Task<IActionResult> GetSalary([FromBody] SalaryRequest request)
    {
        request = request with { UserId = GetUserId(), Mode = request.OfferId != null ? "offer" : "arena" };
        var result = await _arenaService.GetSalaryAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/arena/salary-coach
    /// Chat interactif pour la négociation salariale.
    /// </summary>
    [HttpPost("salary-coach")]
    public async Task<IActionResult> SalaryCoach([FromBody] SalaryCoachRequest request)
    {
        request = request with { UserId = GetUserId(), Mode = request.OfferId != null ? "offer" : "arena" };
        var result = await _arenaService.SalaryCoachAsync(request);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Historique
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/arena/sessions?userId=xxx
    /// Retourne l'historique des sessions de l'utilisateur.
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] string? userId)
    {
        // Si userId n'est pas fourni, on prend celui du token
        var id = string.IsNullOrEmpty(userId) ? GetUserId() : userId;
        var result = await _arenaService.GetSessionsAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/arena/sessions/{sessionId}/details
    /// Retourne les détails (feedback complet) d'une session.
    /// </summary>
    [HttpGet("sessions/{sessionId}/details")]
    public async Task<IActionResult> GetSessionDetails(string sessionId)
    {
        var result = await _arenaService.GetSessionDetailsAsync(sessionId);
        if (result == null) return NotFound(new { error = "Session non trouvée ou sans évaluation." });
        return Ok(result);
    }
}