namespace NextStep.Modules.Applications.Application.Dtos;

public class OfferSubmitResponseDto
{
    public Guid OfferId { get; set; }
    public string Status { get; set; } = "saved";
}
