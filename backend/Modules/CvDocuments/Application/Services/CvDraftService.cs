using NextStep.Modules.CvDocuments.Infrastructure.Rendering;
using System.Text.Json;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.CvDocuments.Application.Dtos;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.CvDocuments.Application.Services;

/// <summary>The editable CV draft of an offer (stored on the application document).</summary>
public interface ICvDraftService
{
    Task<CvDraftDto?> GetCvDraftAsync(Guid userId, Guid offerId, CancellationToken ct = default);
    Task<CvDraftDto> SaveCvDraftAsync(Guid userId, Guid offerId, JsonElement draft, CancellationToken ct = default);
}

public class CvDraftService(IApplicationsApi applications, ICvContentSanitizer sanitizer) : ICvDraftService
{
    private const string DraftNotFound = "CV draft not found.";

    public async Task<CvDraftDto?> GetCvDraftAsync(Guid userId, Guid offerId, CancellationToken ct = default)
    {
        await EnsureOfferOwnedAsync(userId, offerId, ct);
        var document = await applications.GetCvDocumentAsync(userId, offerId, ct);
        return document is null ? null : ToDto(offerId, document);
    }

    public async Task<CvDraftDto> SaveCvDraftAsync(Guid userId, Guid offerId, JsonElement draft, CancellationToken ct = default)
    {
        await EnsureOfferOwnedAsync(userId, offerId, ct);

        string sanitized;
        try
        {
            sanitized = sanitizer.Sanitize(draft);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new BadRequestException(ex.Message);
        }

        var document = await applications.SaveCvDocumentAsync(userId, offerId, sanitized, pdfUrl: null, createApplicationIfMissing: true, ct)
            ?? throw new NotFoundException(DraftNotFound);
        return ToDto(offerId, document);
    }

    private async Task EnsureOfferOwnedAsync(Guid userId, Guid offerId, CancellationToken ct)
    {
        try
        {
            await applications.EnsureOfferOwnedAsync(userId, offerId, ct);
        }
        catch (KeyNotFoundException)
        {
            throw new NotFoundException(DraftNotFound);
        }
    }

    private static CvDraftDto ToDto(Guid offerId, StoredCvDocument document) => new()
    {
        OfferId = offerId,
        Data = JsonSerializer.Deserialize<object>(document.CvJson),
        Version = document.Version,
        UpdatedAtUtc = document.UpdatedAtUtc,
    };
}
