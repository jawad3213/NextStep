using Microsoft.AspNetCore.SignalR;
using NextStep.Modules.Cv.Services;
using NextStep.Modules.Offer.DTOs;
using NextStep.SignalR;

namespace NextStep.Modules.Offer.Services;

public interface IPdfGenerationService
{
    Task<PdfGenerateResultDto> GeneratePdfAsync(
        Guid userId, Guid offerId, string templateId, CancellationToken ct = default);
}

public class PdfGenerationService : IPdfGenerationService
{
    private readonly ICvService _cvService;
    private readonly IHubContext<PipelineHub> _hubContext;
    private readonly ILogger<PdfGenerationService> _logger;

    public PdfGenerationService(
        ICvService cvService,
        IHubContext<PipelineHub> hubContext,
        ILogger<PdfGenerationService> logger)
    {
        _cvService = cvService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<PdfGenerateResultDto> GeneratePdfAsync(
        Guid userId, Guid offerId, string templateId, CancellationToken ct = default)
    {
        _logger.LogInformation("PDFGen — Generating PDF for offer {OfferId}, template {Template}", offerId, templateId);

        try
        {
            await SendProgress(offerId, 10, "Préparation des données CV...");

            var preview = await _cvService.PreviewCvAsync(userId, templateId, offerId);

            await SendProgress(offerId, 50, "Génération du PDF avec QuestPDF...");

            var saveResult = await _cvService.SaveCvAsync(userId, new CvSaveRequest
            {
                TemplateSlug = templateId,
                Title = $"CV_{offerId}",
                Data = preview.Data
            });

            await SendProgress(offerId, 100, "PDF généré avec succès !");

            var result = new PdfGenerateResultDto
            {
                OfferId = offerId,
                DownloadUrl = saveResult.FileUrl,
                Status = "completed"
            };

            await _hubContext.Clients.Group(offerId.ToString())
                .SendAsync("GenerationCompleted", result, CancellationToken.None);

            _logger.LogInformation("PDFGen — ✅ PDF done for offer {OfferId}", offerId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDFGen — ❌ PDF generation failed for offer {OfferId}", offerId);

            await _hubContext.Clients.Group(offerId.ToString())
                .SendAsync("GenerationError", new
                {
                    OfferId = offerId,
                    Error = ex.Message,
                    Status = "error"
                }, CancellationToken.None);

            throw;
        }
    }

    private async Task SendProgress(Guid offerId, int percent, string message)
    {
        var dto = new GenerationProgressDto
        {
            OfferId = offerId,
            ProgressPercent = percent,
            Message = message,
            Status = "running"
        };

        await _hubContext.Clients.Group(offerId.ToString())
            .SendAsync("GenerationProgress", dto);
    }
}
