using NextStep.Modules.CvDocuments.Infrastructure.Rendering;
using Microsoft.EntityFrameworkCore;
using NextStep.Modules.CvDocuments.Contracts;
using NextStep.Modules.CvDocuments.Infrastructure.Persistence;

namespace NextStep.Modules.CvDocuments.Application.Services;

/// <summary>In-process implementation of the CV Documents module contract.</summary>
public class CvDocumentsApi(CvDocumentsDbContext db, ICvService cvService) : ICvDocumentsApi
{
    public Task<Guid?> FindLatestFinalCvAsync(Guid userId, Guid offerId, CancellationToken ct = default) =>
        db.CvHistories
            .AsNoTracking()
            .Where(h => h.UserId == userId && h.Title == $"CV_{offerId}")
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => (Guid?)h.Id)
            .FirstOrDefaultAsync(ct);

    public Task<byte[]> GetCvPdfBytesAsync(Guid userId, Guid cvHistoryId, CancellationToken ct = default) =>
        cvService.GetDownloadBytesAsync(userId, cvHistoryId);
}
