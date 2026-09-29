using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Mappings;
using NextStep.Modules.Applications.Domain;
using NextStep.Modules.Applications.Infrastructure.Persistence;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Applications.Application.Services;

/// <summary>
/// What the Python agents (SN Copilot, interview coach) may read and change in the
/// Applications module. Writes go through <see cref="ICandidatureService"/>, so the same
/// rules and status history apply as for the user's own actions.
/// </summary>
public interface IAgentApplicationsService
{
    Task<List<AgentCandidatureDto>> ListAsync(Guid userId, int limit, CancellationToken ct = default);
    Task<AgentCandidatureDto> CreateAsync(Guid userId, AgentCreateCandidatureDto dto, CancellationToken ct = default);
    Task<AgentCandidatureDto> UpdateStatusAsync(Guid userId, Guid candidatureId, AgentStatusUpdateDto dto, CancellationToken ct = default);
    Task AddNoteAsync(Guid userId, Guid candidatureId, AgentNoteDto dto, CancellationToken ct = default);

    /// <summary>The stored analysis JSON of the user's offer, or null.</summary>
    Task<string?> GetOfferAnalysisJsonAsync(Guid userId, Guid offerId, CancellationToken ct = default);
}

public class AgentApplicationsService(ICandidatureService candidatures, ApplicationsDbContext db) : IAgentApplicationsService
{
    /// <summary>Recorded in the status history for changes made by the copilot.</summary>
    public const string AgentSource = "ai_sn";
    private const string UnknownCompany = "Company not specified";
    private const string UnknownRole = "Position not specified";

    public async Task<List<AgentCandidatureDto>> ListAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        var items = (await candidatures.GetByUserIdAsync(userId, ct))
            .OrderByDescending(c => c.ApplicationDate)
            .ThenByDescending(c => c.DateCreation)
            .Take(Math.Clamp(limit, 1, 200))
            .ToList();

        var offerIds = items.Where(c => c.IdOffre.HasValue).Select(c => c.IdOffre!.Value).Distinct().ToList();
        var offers = await db.OffresEmploi
            .Where(o => offerIds.Contains(o.Id))
            .Select(o => new { o.Id, o.AnalyseJson, o.TexteBrut })
            .ToListAsync(ct);
        var titles = offers.ToDictionary(o => o.Id, o => OfferTitle(o.Id, o.AnalyseJson, o.TexteBrut));

        return items.Select(c =>
        {
            var (company, role) = c.IdOffre is { } offerId && titles.TryGetValue(offerId, out var t) ? t : (null, null);
            var (noteCompany, noteRole) = FromNotes(c.Notes);
            return ToAgentDto(c, company ?? noteCompany ?? UnknownCompany, role ?? noteRole ?? UnknownRole);
        }).ToList();
    }

    public async Task<AgentCandidatureDto> CreateAsync(Guid userId, AgentCreateCandidatureDto dto, CancellationToken ct = default)
    {
        var poste = string.IsNullOrWhiteSpace(dto.Poste) ? UnknownRole : dto.Poste.Trim();
        // Same notes convention as the board: company, role, then free text.
        var notes = string.Join('\n', new[] { dto.Entreprise.Trim(), poste, dto.Notes?.Trim() }
            .Where(line => !string.IsNullOrEmpty(line)));

        var created = await candidatures.CreateAsync(userId, new CreateCandidatureDto
        {
            Entreprise = dto.Entreprise.Trim(),
            Poste = poste,
            Channel = string.IsNullOrWhiteSpace(dto.Channel) ? "EMAIL" : dto.Channel.Trim().ToUpperInvariant(),
            AppliedManually = true,
            Notes = notes,
        }, AgentSource, ct);
        return ToAgentDto(created, dto.Entreprise.Trim(), poste);
    }

    public async Task<AgentCandidatureDto> UpdateStatusAsync(Guid userId, Guid candidatureId, AgentStatusUpdateDto dto, CancellationToken ct = default)
    {
        var status = dto.NouveauStatut.Trim().ToUpperInvariant();
        if (!CandidatureStatuses.All.Contains(status))
            throw new BadRequestException($"Statut inconnu : {dto.NouveauStatut}.");

        await candidatures.GetOwnedAsync(userId, candidatureId, ct);
        var updated = await candidatures.UpdateStatutAsync(
            candidatureId,
            new UpdateStatutDto { NouveauStatut = status, Details = dto.Details ?? "Updated via SN Copilot" },
            AgentSource,
            ct) ?? throw new NotFoundException("Application not found.");

        return (await ListAsync(userId, 200, ct)).FirstOrDefault(c => c.Id == updated.IdCandidature)
               ?? ToAgentDto(updated, UnknownCompany, UnknownRole);
    }

    public async Task AddNoteAsync(Guid userId, Guid candidatureId, AgentNoteDto dto, CancellationToken ct = default)
    {
        await candidatures.GetOwnedAsync(userId, candidatureId, ct);
        await candidatures.AddNoteAsync(candidatureId, new AddNoteDto { Contenu = dto.Contenu.Trim(), Auteur = "ai" }, ct);
    }

    public Task<string?> GetOfferAnalysisJsonAsync(Guid userId, Guid offerId, CancellationToken ct = default) =>
        db.OffresEmploi
            .Where(o => o.Id == offerId && o.UtilisateurId == userId)
            .Select(o => o.AnalyseJson)
            .FirstOrDefaultAsync(ct);

    private static (string? Company, string? Role) OfferTitle(Guid offerId, string? analyseJson, string? rawText)
    {
        if (string.IsNullOrWhiteSpace(analyseJson)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(analyseJson);
            var analysis = OfferAnalysisMapper.ToAnalysisDto(offerId, doc.RootElement, rawText);
            return (NullIfBlank(analysis.Entreprise), NullIfBlank(analysis.Titre));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static (string? Company, string? Role) FromNotes(string? notes)
    {
        var lines = (notes ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (lines.ElementAtOrDefault(0), lines.ElementAtOrDefault(1));
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AgentCandidatureDto ToAgentDto(CandidatureDto c, string company, string role) => new()
    {
        Id = c.IdCandidature,
        IdOffre = c.IdOffre,
        Entreprise = company,
        Role = role,
        Statut = c.Statut,
        Channel = c.Channel,
        ChannelUrl = c.ChannelUrl,
        ApplicationDate = c.ApplicationDate,
        HasResponse = c.HasResponse,
        ResponseStatus = c.ResponseStatus,
        Notes = c.Notes,
        ResponseSummary = c.ResponseSummary,
        RecommendedAction = c.RecommendedAction,
        FollowUpNeeded = c.FollowUpNeeded,
    };
}
