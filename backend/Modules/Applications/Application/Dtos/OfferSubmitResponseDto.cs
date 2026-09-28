namespace NextStep.Modules.Offer.DTOs;

public class OfferSubmitResponseDto
{
    public Guid OfferId { get; set; }
    public string Status { get; set; } = "saved";
}
