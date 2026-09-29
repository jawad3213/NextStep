using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Applications.Infrastructure.Persistence;
using NextStep.Modules.Applications.Domain;

namespace NextStep.Modules.Applications.Application.Services;

/// <summary>In-process implementation of the Applications module contract.</summary>
public class ApplicationsApi(ApplicationsDbContext db, IOfferService offerService) : IApplicationsApi
{
    private static readonly string[] FollowUpStatuses = ["REPONSE_RECUE", "RELANCE_NECESSAIRE", "RELANCE_GENEREE"];

    public async Task<OfferSummary?> GetOfferSummaryAsync(Guid userId, Guid offerId, CancellationToken ct = default)
    {
        var analysis = await offerService.GetAnalysisAsync(userId, offerId, ct);
        return analysis is null
            ? null
            : new OfferSummary(offerId, analysis.Titre, analysis.Entreprise, analysis.DescriptionPoste,
                analysis.CompetencesRequises, analysis.KeywordsAts,
                Location: analysis.Localisation,
                ContractType: analysis.TypeContrat,
                YearsExperience: analysis.AnneesExperience,
                MatchingScore: analysis.MatchResult is null && analysis.SkillGapAnalysis is null ? null : analysis.ScoreMatching,
                AnalysedAt: analysis.DateAnalyse);
    }

    public async Task EnsureOfferOwnedAsync(Guid userId, Guid offerId, CancellationToken ct = default)
    {
        var exists = await db.OffresEmploi.AnyAsync(o => o.Id == offerId && o.UtilisateurId == userId, ct);
        if (!exists) throw new KeyNotFoundException($"Offer {offerId} not found.");
    }

    public async Task<StoredCvDocument?> GetCvDocumentAsync(Guid userId, Guid offerId, CancellationToken ct = default)
    {
        var document = await (
            from c in db.Candidatures
            join d in db.DocumentsGeneres on c.IdCandidature equals d.IdCandidature
            where c.IdOffre == offerId && c.IdUtilisateur == userId
            select d
        ).FirstOrDefaultAsync(ct);

        return document?.CvContenuIaJson is null
            ? null
            : new StoredCvDocument(document.CvContenuIaJson, document.Version, document.DateGeneration);
    }

    public async Task<StoredCvDocument?> SaveCvDocumentAsync(
        Guid userId,
        Guid offerId,
        string cvJson,
        string? pdfUrl,
        bool createApplicationIfMissing,
        CancellationToken ct = default)
    {
        var candidature = await db.Candidatures
            .FirstOrDefaultAsync(c => c.IdOffre == offerId && c.IdUtilisateur == userId, ct);

        if (candidature == null)
        {
            if (!createApplicationIfMissing) return null;

            var offer = await db.OffresEmploi
                .FirstOrDefaultAsync(o => o.Id == offerId && o.UtilisateurId == userId, ct);
            if (offer is null) return null;

            candidature = new Candidature
            {
                IdUtilisateur = userId,
                IdOffre = offerId,
                Offre = offer,
                Statut = "EN_ATTENTE",
                DateCreation = DateTime.UtcNow
            };
            db.Candidatures.Add(candidature);
            await db.SaveChangesAsync(ct);
        }

        var document = await db.DocumentsGeneres
            .FirstOrDefaultAsync(d => d.IdCandidature == candidature.IdCandidature, ct);

        var now = DateTime.UtcNow;
        if (document == null)
        {
            document = new DocumentGenere
            {
                IdCandidature = candidature.IdCandidature,
                CvContenuIaJson = cvJson,
                CheminPdfCv = pdfUrl,
                Version = 1,
                DateGeneration = now,
            };
            db.DocumentsGeneres.Add(document);
        }
        else
        {
            document.CvContenuIaJson = cvJson;
            if (pdfUrl is not null) document.CheminPdfCv = pdfUrl;
            document.Version += 1;
            document.DateGeneration = now;
        }

        await db.SaveChangesAsync(ct);
        return new StoredCvDocument(cvJson, document.Version, document.DateGeneration);
    }

    // ── Offers ──────────────────────────────────────────────────────────────

    public Task<bool> OfferExistsAsync(Guid offerId, CancellationToken ct = default) =>
        db.OffresEmploi.AnyAsync(o => o.Id == offerId, ct);

    public async Task<OfferContent?> GetOfferContentAsync(Guid offerId, CancellationToken ct = default)
    {
        return await db.OffresEmploi
            .AsNoTracking()
            .Where(o => o.Id == offerId)
            .Select(o => new OfferContent(o.Id, o.UtilisateurId, o.TexteBrut, o.AnalyseJson))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Guid> CreateOfferFromTextAsync(Guid userId, string rawText, CancellationToken ct = default)
    {
        var offer = await offerService.SaveOfferAsync(rawText, userId.ToString(), ct);
        return offer.Id;
    }

    // ── Applications (candidatures) ─────────────────────────────────────────

    public async Task<ApplicationSnapshot?> GetApplicationAsync(Guid candidatureId, CancellationToken ct = default)
    {
        return await Snapshots(db.Candidatures.Where(c => c.IdCandidature == candidatureId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ApplicationSnapshot>> GetApplicationsAsync(
        IReadOnlyCollection<Guid> candidatureIds,
        CancellationToken ct = default)
    {
        if (candidatureIds.Count == 0) return [];
        return await Snapshots(db.Candidatures.Where(c => candidatureIds.Contains(c.IdCandidature)))
            .ToListAsync(ct);
    }

    public async Task<ApplicationSnapshot?> FindApplicationForOfferAsync(Guid userId, Guid offerId, CancellationToken ct = default)
    {
        return await Snapshots(db.Candidatures.Where(c => c.IdOffre == offerId && c.IdUtilisateur == userId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ApplicationSnapshot?> FindFirstApplicationAsync(Guid userId, CancellationToken ct = default)
    {
        return await Snapshots(db.Candidatures
                .Where(c => c.IdUtilisateur == userId)
                .OrderBy(c => c.DateCreation))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListAppliedOfferIdsAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Candidatures
            .Where(c => c.IdUtilisateur == userId && c.IdOffre.HasValue)
            .Select(c => c.IdOffre!.Value)
            .ToListAsync(ct);
    }

    public async Task<Guid> EnsureApplicationForOfferAsync(
        Guid userId,
        Guid offerId,
        string offerTextIfMissing,
        string status,
        CancellationToken ct = default)
    {
        if (!await db.OffresEmploi.AnyAsync(o => o.Id == offerId, ct))
        {
            db.OffresEmploi.Add(new OffreEmploi
            {
                Id = offerId,
                UtilisateurId = userId,
                TexteBrut = offerTextIfMissing,
                AnalyseJson = null,
                DateCreation = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        var existingId = await db.Candidatures
            .Where(c => c.IdUtilisateur == userId && c.IdOffre == offerId)
            .Select(c => (Guid?)c.IdCandidature)
            .FirstOrDefaultAsync(ct);
        if (existingId.HasValue) return existingId.Value;

        var candidature = new Candidature
        {
            IdCandidature = Guid.NewGuid(),
            IdUtilisateur = userId,
            IdOffre = offerId,
            Statut = status,
            DateCreation = DateTime.UtcNow
        };
        db.Candidatures.Add(candidature);
        await db.SaveChangesAsync(ct);
        return candidature.IdCandidature;
    }

    // ── Response tracking (updated by Messaging) ────────────────────────────

    public async Task<IReadOnlyList<ApplicationSnapshot>> ListAwaitingFollowUpCheckAsync(CancellationToken ct = default)
    {
        return await Snapshots(db.Candidatures
                .Where(c => !c.HasResponse && !FollowUpStatuses.Contains(c.ResponseStatus)))
            .ToListAsync(ct);
    }

    public async Task SetFollowUpStatusAsync(Guid candidatureId, string status, CancellationToken ct = default)
    {
        var candidature = await db.Candidatures.FirstOrDefaultAsync(c => c.IdCandidature == candidatureId, ct);
        if (candidature is null) return;

        candidature.ResponseStatus = status;
        candidature.Statut = status;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFollowUpNeededAsync(Guid candidatureId, DateTime? lastFollowUpAtUtc, CancellationToken ct = default)
    {
        var candidature = await db.Candidatures.FirstOrDefaultAsync(c => c.IdCandidature == candidatureId, ct);
        if (candidature is null) return;

        candidature.ResponseStatus = "RELANCE_NECESSAIRE";
        candidature.Statut = "RELANCE_NECESSAIRE";
        candidature.FollowUpNeeded = true;
        if (lastFollowUpAtUtc.HasValue)
            candidature.LastFollowUpAtUtc = lastFollowUpAtUtc;
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordReplyCheckAsync(Guid candidatureId, DateTime checkedAtUtc, CancellationToken ct = default)
    {
        var candidature = await db.Candidatures.FirstOrDefaultAsync(c => c.IdCandidature == candidatureId, ct);
        if (candidature is null) return;

        candidature.LastCheckedAtUtc = checkedAtUtc;
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordRecruiterReplyAsync(Guid candidatureId, RecruiterReply reply, CancellationToken ct = default)
    {
        var candidature = await db.Candidatures.FirstOrDefaultAsync(c => c.IdCandidature == candidatureId, ct);
        if (candidature is null) return;

        candidature.HasResponse = true;
        candidature.ResponseStatus = reply.Status;
        candidature.Statut = reply.Status;
        candidature.LastResponseAtUtc = reply.ReplyAtUtc;
        candidature.LastCheckedAtUtc = reply.CheckedAtUtc;
        candidature.LastResponseFrom = reply.From;
        candidature.LastResponseSnippet = reply.Snippet;
        candidature.ResponseSummary = reply.Summary;
        candidature.RecommendedAction = reply.RecommendedAction;
        candidature.ResponseConfidence = reply.Confidence;
        candidature.ResponseClassifiedAtUtc = reply.ClassifiedAtUtc;
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<ApplicationSnapshot> Snapshots(IQueryable<Candidature> query) =>
        query.AsNoTracking().Select(c => new ApplicationSnapshot(
            c.IdCandidature,
            c.IdUtilisateur,
            c.IdOffre,
            c.Statut,
            c.ResponseStatus,
            c.HasResponse,
            c.ResponseConfidence,
            c.LastResponseFrom,
            c.LastResponseSnippet,
            c.LastResponseAtUtc,
            c.ResponseSummary,
            c.RecommendedAction));
}
