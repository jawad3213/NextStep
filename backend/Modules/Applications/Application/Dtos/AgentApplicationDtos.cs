using System.ComponentModel.DataAnnotations;

namespace NextStep.Modules.Applications.Application.Dtos;

/// <summary>An application as the SN Copilot agent lists it (company and role resolved).</summary>
public sealed record AgentCandidatureDto
{
    public Guid Id { get; init; }
    public Guid? IdOffre { get; init; }
    public string Entreprise { get; init; } = "";
    public string Role { get; init; } = "";
    public string Statut { get; init; } = "";
    public string Channel { get; init; } = "EMAIL";
    public string? ChannelUrl { get; init; }
    public DateTime ApplicationDate { get; init; }
    public bool HasResponse { get; init; }
    public string ResponseStatus { get; init; } = "";
    public string? Notes { get; init; }
    public string? ResponseSummary { get; init; }
    public string? RecommendedAction { get; init; }
    public bool FollowUpNeeded { get; init; }
}

public class AgentCreateCandidatureDto
{
    [Required]
    public string Entreprise { get; set; } = string.Empty;

    public string? Poste { get; set; }

    public string Channel { get; set; } = "EMAIL";

    public string? Notes { get; set; }
}

public class AgentStatusUpdateDto
{
    [Required]
    public string NouveauStatut { get; set; } = string.Empty;

    public string? Details { get; set; }
}

public class AgentNoteDto
{
    [Required]
    public string Contenu { get; set; } = string.Empty;
}
