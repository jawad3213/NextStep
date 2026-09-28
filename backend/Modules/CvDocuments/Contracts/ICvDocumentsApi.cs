namespace NextStep.Modules.CvDocuments.Contracts;

/// <summary>
/// Public contract of the CV Documents module (saved CVs and their PDFs).
/// Other modules must use this interface instead of CvDocuments' entities or services.
/// </summary>
public interface ICvDocumentsApi
{
    /// <summary>Id of the latest final CV saved for this offer (title "CV_{offerId}"), or null.</summary>
    Task<Guid?> FindLatestFinalCvAsync(Guid userId, Guid offerId, CancellationToken ct = default);

    /// <summary>PDF bytes of a saved CV (empty if the file is missing).</summary>
    Task<byte[]> GetCvPdfBytesAsync(Guid userId, Guid cvHistoryId, CancellationToken ct = default);
}
