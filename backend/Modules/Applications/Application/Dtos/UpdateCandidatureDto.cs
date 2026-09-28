namespace NextStep.Modules.Candidature.DTOs;

public class UpdateCandidatureDto
{
    public string? Channel { get; set; }

    public string? ChannelUrl { get; set; }

    public string? ChannelContact { get; set; }

    public string? Notes { get; set; }

    public string? Language { get; set; }

    public bool? InclureLettreMotivation { get; set; }
}
