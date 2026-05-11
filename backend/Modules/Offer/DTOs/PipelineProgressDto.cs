namespace NextStep.Modules.Offer.DTOs;

public class PipelineProgressDto
{
    public Guid OfferId { get; set; }
    public string Step { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public string? Message { get; set; }
    public string? AgentName { get; set; }
}

public class PipelineCompletedDto
{
    public Guid OfferId { get; set; }
    public string Status { get; set; } = "completed";
    public OfferAnalysisDto? Result { get; set; }
    public string? Error { get; set; }
}
