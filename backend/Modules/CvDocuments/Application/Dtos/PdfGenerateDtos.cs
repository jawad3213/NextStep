namespace NextStep.Modules.Offer.DTOs;

public class PdfGenerateDto
{
    public string TemplateId { get; set; } = "modern";
}

public class PdfGenerateResultDto
{
    public Guid OfferId { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "completed";
}

public class GenerationProgressDto
{
    public Guid OfferId { get; set; }
    public int ProgressPercent { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = "running";
}
