namespace NextStep.Modules.CvDocuments.Application.Dtos;

/// <summary>The editable CV draft of an offer.</summary>
public class CvDraftDto
{
    public Guid OfferId { get; set; }
    public object? Data { get; set; }
    public int Version { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
