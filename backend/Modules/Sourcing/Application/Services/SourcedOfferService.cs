using NextStep.Modules.Sourcing.Infrastructure.JobBoards;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Sourcing.Infrastructure.Persistence;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.Sourcing.Application.Dtos;
using NextStep.Modules.Sourcing.Domain;
using NextStep.Modules.Sourcing.Application.Mappings;
using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Sourcing.Application.Services;

public interface ISourcedOfferService
{
    Task<SourcedOfferSearchResponse> SearchAsync(Guid userId, SourcedOfferSearchRequest request, CancellationToken ct = default);
    Task<List<SourcedOfferListItemDto>> ListAsync(Guid userId, SourcedOfferSearchRequest request, CancellationToken ct = default);
    Task<SourcedOfferDetailDto?> GetByIdAsync(Guid userId, Guid sourcedOfferId, CancellationToken ct = default);
    Task<SourcedOfferDetailDto> UpdateAsync(Guid userId, Guid sourcedOfferId, SourcedOfferUpdateRequest request, CancellationToken ct = default);
    Task<PromoteSourcedOfferResponse> PromoteAsync(Guid userId, Guid sourcedOfferId, CancellationToken ct = default);
}

public class SourcedOfferService(
    SourcingDbContext db,
    JobProviderClient jobProviders,
    IApplicationsApi applications,
    ILogger<SourcedOfferService> logger) : ISourcedOfferService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly string[] DefaultProviders = ["linkedin", "indeed", "glassdoor"];

    public async Task<SourcedOfferSearchResponse> SearchAsync(Guid userId, SourcedOfferSearchRequest request, CancellationToken ct = default)
    {
        var normalizedRequest = NormalizeRequest(request);
        var providers = normalizedRequest.Providers.Count == 0 ? DefaultProviders.ToList() : normalizedRequest.Providers;
        var warnings = new List<string>();
        var errors = new List<string>();
        var normalizedOffers = new List<NormalizedProviderOffer>();

        var tasks = providers.Select(provider => jobProviders.SearchAsync(provider, normalizedRequest, ct)).ToList();
        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            if (!string.IsNullOrWhiteSpace(result.Warning))
            {
                warnings.Add(result.Warning);
            }
            warnings.AddRange(result.Warnings);
            errors.AddRange(result.Errors);
            normalizedOffers.AddRange(result.Offers);
        }

        var persistedOffers = new List<SourcedOffer>();
        foreach (var normalizedOffer in normalizedOffers)
        {
            var persisted = await UpsertAsync(userId, normalizedOffer, normalizedRequest, ct);
            persistedOffers.Add(persisted);
        }

        var session = new ScrapeSession
        {
            UserId = userId,
            Keywords = normalizedRequest.Keywords,
            Location = normalizedRequest.Location,
            ProvidersJson = SerializeJson(providers),
            CountryCode = normalizedRequest.IndeedCountryCode,
            PostedWindow = normalizedRequest.PostedWindow,
            ContractTypesJson = SerializeJson(normalizedRequest.ContractTypes),
            Limit = normalizedRequest.Limit,
            ResultCount = persistedOffers.Count,
            WarningsJson = SerializeJson(warnings.Distinct().ToList()),
            ErrorsJson = SerializeJson(errors.Distinct().ToList()),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Add(session);
        await db.SaveChangesAsync(ct);

        return new SourcedOfferSearchResponse
        {
            Session = session.ToDto(),
            Offers = persistedOffers
                .OrderByDescending(x => x.LastSeenAtUtc)
                .Take(normalizedRequest.Limit)
                .Select(o => o.ToListItemDto())
                .ToList(),
            Warnings = warnings.Distinct().ToList(),
        };
    }

    public async Task<List<SourcedOfferListItemDto>> ListAsync(Guid userId, SourcedOfferSearchRequest request, CancellationToken ct = default)
    {
        var normalizedRequest = NormalizeRequest(request);
        var query = db.Set<SourcedOffer>()
            .Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(normalizedRequest.Keywords))
        {
            var keyword = normalizedRequest.Keywords.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Title.ToLower().Contains(keyword) ||
                (x.Company ?? string.Empty).ToLower().Contains(keyword) ||
                (x.Description ?? string.Empty).ToLower().Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(normalizedRequest.Location))
        {
            var location = normalizedRequest.Location.Trim().ToLowerInvariant();
            query = query.Where(x => (x.Location ?? string.Empty).ToLower().Contains(location));
        }

        if (normalizedRequest.Providers.Count > 0)
        {
            query = query.Where(x => normalizedRequest.Providers.Contains(x.Provider));
        }

        if (!string.IsNullOrWhiteSpace(normalizedRequest.PostedWindow) &&
            !string.Equals(normalizedRequest.PostedWindow, PostedWindowValues.Any, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.PostedWindow == normalizedRequest.PostedWindow);
        }

        if (normalizedRequest.ContractTypes.Count > 0)
        {
            query = query.Where(x => normalizedRequest.ContractTypes.Contains(x.NormalizedContractType ?? string.Empty));
        }

        query = normalizedRequest.WorkflowState switch
        {
            "saved" => query.Where(x => x.IsSaved && !x.IsArchived),
            "shortlisted" => query.Where(x => x.IsShortlisted && !x.IsArchived),
            "archived" => query.Where(x => x.IsArchived),
            _ => query.Where(x => !x.IsArchived),
        };

        var offers = await query
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Take(Math.Clamp(normalizedRequest.Limit, 1, 100))
            .ToListAsync(ct);

        return offers.Select(o => o.ToListItemDto()).ToList();
    }

    public async Task<SourcedOfferDetailDto?> GetByIdAsync(Guid userId, Guid sourcedOfferId, CancellationToken ct = default)
    {
        var offer = await db.Set<SourcedOffer>()
            .FirstOrDefaultAsync(x => x.Id == sourcedOfferId && x.UserId == userId, ct);
        if (offer is null)
        {
            return null;
        }

        var detail = offer.ToDetailDto();
        var similarOffers = await db.Set<SourcedOffer>()
            .Where(x => x.UserId == userId && x.Id != sourcedOfferId && x.DedupeKey == offer.DedupeKey)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Take(6)
            .ToListAsync(ct);
        detail.SimilarOffers = similarOffers.Select(o => o.ToListItemDto()).ToList();
        return detail;
    }

    public async Task<SourcedOfferDetailDto> UpdateAsync(Guid userId, Guid sourcedOfferId, SourcedOfferUpdateRequest request, CancellationToken ct = default)
    {
        var offer = await db.Set<SourcedOffer>()
            .FirstOrDefaultAsync(x => x.Id == sourcedOfferId && x.UserId == userId, ct)
            ?? throw new NotFoundException("Sourced offer not found.");

        if (request.IsSaved.HasValue) offer.IsSaved = request.IsSaved.Value;
        if (request.IsShortlisted.HasValue)
        {
            offer.IsShortlisted = request.IsShortlisted.Value;
            if (offer.IsShortlisted) offer.IsSaved = true;
        }
        if (request.IsArchived.HasValue) offer.IsArchived = request.IsArchived.Value;
        offer.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return offer.ToDetailDto();
    }

    public async Task<PromoteSourcedOfferResponse> PromoteAsync(Guid userId, Guid sourcedOfferId, CancellationToken ct = default)
    {
        var sourcedOffer = await db.Set<SourcedOffer>()
            .FirstOrDefaultAsync(x => x.Id == sourcedOfferId && x.UserId == userId, ct)
            ?? throw new NotFoundException("Sourced offer not found.");

        if (sourcedOffer.PromotedOfferId.HasValue)
        {
            return new PromoteSourcedOfferResponse
            {
                OfferId = sourcedOffer.PromotedOfferId.Value,
                AlreadyPromoted = true,
            };
        }

        var rawText = BuildRawOfferText(sourcedOffer);
        var offerId = await applications.CreateOfferFromTextAsync(userId, rawText, ct);
        sourcedOffer.PromotedOfferId = offerId;
        sourcedOffer.UpdatedAtUtc = DateTime.UtcNow;
        sourcedOffer.IsSaved = true;
        await db.SaveChangesAsync(ct);

        return new PromoteSourcedOfferResponse
        {
            OfferId = offerId,
            AlreadyPromoted = false,
        };
    }

    private async Task<SourcedOffer> UpsertAsync(Guid userId, NormalizedProviderOffer normalizedOffer, SourcedOfferSearchRequest request, CancellationToken ct)
    {
        var existing = await FindExistingAsync(userId, normalizedOffer, ct);
        var now = DateTime.UtcNow;
        if (existing is null)
        {
            existing = new SourcedOffer
            {
                UserId = userId,
                Provider = normalizedOffer.Provider,
                ProviderJobId = normalizedOffer.ProviderJobId,
                ExternalUrl = normalizedOffer.ExternalUrl,
                Title = normalizedOffer.Title,
                Company = normalizedOffer.Company,
                Location = normalizedOffer.Location,
                Description = normalizedOffer.Description,
                PostedAtText = normalizedOffer.PostedAtText,
                PostedWindow = normalizedOffer.PostedWindow,
                RawContractType = normalizedOffer.RawContractType,
                NormalizedContractType = normalizedOffer.NormalizedContractType,
                EmploymentType = normalizedOffer.EmploymentType,
                SeniorityLevel = normalizedOffer.SeniorityLevel,
                MatchedItTermsJson = SerializeJson(normalizedOffer.MatchedItTerms),
                SourceQueryJson = SerializeJson(BuildSourceQuery(request)),
                DedupeKey = normalizedOffer.DedupeKey,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                ScrapedAtUtc = now,
                CreatedAtUtc = now,
            };
            db.Add(existing);
        }
        else
        {
            existing.ExternalUrl = normalizedOffer.ExternalUrl;
            existing.Title = normalizedOffer.Title;
            existing.Company = normalizedOffer.Company;
            existing.Location = normalizedOffer.Location;
            existing.Description = normalizedOffer.Description;
            existing.PostedAtText = normalizedOffer.PostedAtText;
            existing.PostedWindow = normalizedOffer.PostedWindow;
            existing.RawContractType = normalizedOffer.RawContractType;
            existing.NormalizedContractType = normalizedOffer.NormalizedContractType;
            existing.EmploymentType = normalizedOffer.EmploymentType;
            existing.SeniorityLevel = normalizedOffer.SeniorityLevel;
            existing.MatchedItTermsJson = SerializeJson(normalizedOffer.MatchedItTerms);
            existing.SourceQueryJson = SerializeJson(BuildSourceQuery(request));
            existing.LastSeenAtUtc = now;
            existing.ScrapedAtUtc = now;
            existing.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
        return existing;
    }

    private async Task<SourcedOffer?> FindExistingAsync(Guid userId, NormalizedProviderOffer normalizedOffer, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(normalizedOffer.ProviderJobId))
        {
            var byProviderId = await db.Set<SourcedOffer>()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId &&
                    x.Provider == normalizedOffer.Provider &&
                    x.ProviderJobId == normalizedOffer.ProviderJobId,
                    ct);
            if (byProviderId is not null) return byProviderId;
        }

        return await db.Set<SourcedOffer>()
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.Provider == normalizedOffer.Provider &&
                x.DedupeKey == normalizedOffer.DedupeKey,
                ct);
    }

    private static Dictionary<string, object?> BuildSourceQuery(SourcedOfferSearchRequest request) =>
        new()
        {
            ["keywords"] = request.Keywords,
            ["location"] = request.Location,
            ["providers"] = request.Providers,
            ["limit"] = request.Limit,
            ["postedWindow"] = request.PostedWindow,
            ["contractTypes"] = request.ContractTypes,
            ["indeedCountryCode"] = request.IndeedCountryCode,
        };

    private static string BuildRawOfferText(SourcedOffer offer)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Job title: {offer.Title}");
        if (!string.IsNullOrWhiteSpace(offer.Company)) builder.AppendLine($"Company: {offer.Company}");
        if (!string.IsNullOrWhiteSpace(offer.Location)) builder.AppendLine($"Location: {offer.Location}");
        if (!string.IsNullOrWhiteSpace(offer.NormalizedContractType)) builder.AppendLine($"Contract type: {offer.NormalizedContractType}");
        if (!string.IsNullOrWhiteSpace(offer.PostedAtText)) builder.AppendLine($"Posted: {offer.PostedAtText}");
        builder.AppendLine($"Provider: {offer.Provider}");
        if (!string.IsNullOrWhiteSpace(offer.ExternalUrl)) builder.AppendLine($"External URL: {offer.ExternalUrl}");
        builder.AppendLine();
        builder.AppendLine(offer.Description ?? string.Empty);
        return builder.ToString().Trim();
    }

    private static string SerializeJson<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SourcedOfferSearchRequest NormalizeRequest(SourcedOfferSearchRequest request)
    {
        request.Providers = request.Providers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        request.ContractTypes = request.ContractTypes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        request.PostedWindow = string.IsNullOrWhiteSpace(request.PostedWindow) ? PostedWindowValues.Any : request.PostedWindow.Trim().ToLowerInvariant();
        request.Limit = Math.Clamp(request.Limit, 1, 100);
        request.WorkflowState = string.IsNullOrWhiteSpace(request.WorkflowState) ? null : request.WorkflowState.Trim().ToLowerInvariant();
        return request;
    }

}
