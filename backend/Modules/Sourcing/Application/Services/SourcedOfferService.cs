using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextStep.data;
using NextStep.Modules.Offer.Services;
using NextStep.Modules.Sourcing.DTOs;
using NextStep.Modules.Sourcing.Models;
using NextStep.Shared.Http;

namespace NextStep.Modules.Sourcing.Services;

public interface ISourcedOfferService
{
    Task<SourcedOfferSearchResponse> SearchAsync(Guid userId, SourcedOfferSearchRequest request, CancellationToken ct = default);
    Task<List<SourcedOfferListItemDto>> ListAsync(Guid userId, SourcedOfferSearchRequest request, CancellationToken ct = default);
    Task<SourcedOfferDetailDto?> GetByIdAsync(Guid userId, Guid sourcedOfferId, CancellationToken ct = default);
    Task<SourcedOfferDetailDto> UpdateAsync(Guid userId, Guid sourcedOfferId, SourcedOfferUpdateRequest request, CancellationToken ct = default);
    Task<PromoteSourcedOfferResponse> PromoteAsync(Guid userId, Guid sourcedOfferId, CancellationToken ct = default);
}

public class SourcedOfferService(
    AppDbContext db,
    IAgentHttpClient agentHttpClient,
    IOfferService offerService,
    ILogger<SourcedOfferService> logger) : ISourcedOfferService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly string[] DefaultProviders = ["linkedin", "indeed", "glassdoor"];
    private static bool _schemaEnsured;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    public async Task<SourcedOfferSearchResponse> SearchAsync(Guid userId, SourcedOfferSearchRequest request, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var normalizedRequest = NormalizeRequest(request);
        var providers = normalizedRequest.Providers.Count == 0 ? DefaultProviders.ToList() : normalizedRequest.Providers;
        var warnings = new List<string>();
        var errors = new List<string>();
        var normalizedOffers = new List<NormalizedProviderOffer>();

        var tasks = providers.Select(provider => SearchProviderAsync(provider, normalizedRequest, ct)).ToList();
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
            Session = MapSession(session),
            Offers = persistedOffers
                .OrderByDescending(x => x.LastSeenAtUtc)
                .Take(normalizedRequest.Limit)
                .Select(MapListItem)
                .ToList(),
            Warnings = warnings.Distinct().ToList(),
        };
    }

    public async Task<List<SourcedOfferListItemDto>> ListAsync(Guid userId, SourcedOfferSearchRequest request, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
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

        return offers.Select(MapListItem).ToList();
    }

    public async Task<SourcedOfferDetailDto?> GetByIdAsync(Guid userId, Guid sourcedOfferId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var offer = await db.Set<SourcedOffer>()
            .FirstOrDefaultAsync(x => x.Id == sourcedOfferId && x.UserId == userId, ct);
        if (offer is null)
        {
            return null;
        }

        var detail = MapDetail(offer);
        var similarOffers = await db.Set<SourcedOffer>()
            .Where(x => x.UserId == userId && x.Id != sourcedOfferId && x.DedupeKey == offer.DedupeKey)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Take(6)
            .ToListAsync(ct);
        detail.SimilarOffers = similarOffers.Select(MapListItem).ToList();
        return detail;
    }

    public async Task<SourcedOfferDetailDto> UpdateAsync(Guid userId, Guid sourcedOfferId, SourcedOfferUpdateRequest request, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var offer = await db.Set<SourcedOffer>()
            .FirstOrDefaultAsync(x => x.Id == sourcedOfferId && x.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Sourced offer not found.");

        if (request.IsSaved.HasValue) offer.IsSaved = request.IsSaved.Value;
        if (request.IsShortlisted.HasValue)
        {
            offer.IsShortlisted = request.IsShortlisted.Value;
            if (offer.IsShortlisted) offer.IsSaved = true;
        }
        if (request.IsArchived.HasValue) offer.IsArchived = request.IsArchived.Value;
        offer.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return MapDetail(offer);
    }

    public async Task<PromoteSourcedOfferResponse> PromoteAsync(Guid userId, Guid sourcedOfferId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var sourcedOffer = await db.Set<SourcedOffer>()
            .FirstOrDefaultAsync(x => x.Id == sourcedOfferId && x.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Sourced offer not found.");

        if (sourcedOffer.PromotedOfferId.HasValue)
        {
            return new PromoteSourcedOfferResponse
            {
                OfferId = sourcedOffer.PromotedOfferId.Value,
                AlreadyPromoted = true,
            };
        }

        var rawText = BuildRawOfferText(sourcedOffer);
        var created = await offerService.SaveOfferAsync(rawText, userId.ToString(), ct);
        sourcedOffer.PromotedOfferId = created.Id;
        sourcedOffer.UpdatedAtUtc = DateTime.UtcNow;
        sourcedOffer.IsSaved = true;
        await db.SaveChangesAsync(ct);

        return new PromoteSourcedOfferResponse
        {
            OfferId = created.Id,
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

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaEnsured)
        {
            return;
        }

        await SchemaLock.WaitAsync(ct);
        try
        {
            if (_schemaEnsured)
            {
                return;
            }

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS public.sourced_offer (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    user_id UUID NOT NULL,
                    provider TEXT NOT NULL,
                    provider_job_id TEXT NULL,
                    external_url TEXT NULL,
                    title TEXT NOT NULL,
                    company TEXT NULL,
                    location TEXT NULL,
                    description TEXT NULL,
                    posted_at_text TEXT NULL,
                    posted_window TEXT NULL,
                    raw_contract_type TEXT NULL,
                    normalized_contract_type TEXT NULL,
                    employment_type TEXT NULL,
                    seniority_level TEXT NULL,
                    matched_it_terms_json JSONB NOT NULL DEFAULT '[]'::jsonb,
                    source_query_json JSONB NOT NULL DEFAULT '{{}}'::jsonb,
                    dedupe_key TEXT NOT NULL,
                    is_saved BOOLEAN NOT NULL DEFAULT FALSE,
                    is_shortlisted BOOLEAN NOT NULL DEFAULT FALSE,
                    is_archived BOOLEAN NOT NULL DEFAULT FALSE,
                    promoted_offer_id UUID NULL,
                    first_seen_at_utc TIMESTAMP NOT NULL DEFAULT now(),
                    last_seen_at_utc TIMESTAMP NOT NULL DEFAULT now(),
                    scraped_at_utc TIMESTAMP NOT NULL DEFAULT now(),
                    created_at_utc TIMESTAMP NOT NULL DEFAULT now(),
                    updated_at_utc TIMESTAMP NULL
                );

                CREATE TABLE IF NOT EXISTS public.scrape_session (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    user_id UUID NOT NULL,
                    keywords TEXT NULL,
                    location TEXT NULL,
                    providers_json JSONB NOT NULL DEFAULT '[]'::jsonb,
                    country_code TEXT NULL,
                    posted_window TEXT NULL,
                    contract_types_json JSONB NOT NULL DEFAULT '[]'::jsonb,
                    limit_value INTEGER NOT NULL DEFAULT 20,
                    result_count INTEGER NOT NULL DEFAULT 0,
                    warnings_json JSONB NOT NULL DEFAULT '[]'::jsonb,
                    errors_json JSONB NOT NULL DEFAULT '[]'::jsonb,
                    created_at_utc TIMESTAMP NOT NULL DEFAULT now()
                );
            ", cancellationToken: ct);

            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS ix_sourced_offer_user_provider_job ON public.sourced_offer (user_id, provider, provider_job_id);",
                cancellationToken: ct);
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS ix_sourced_offer_user_dedupe ON public.sourced_offer (user_id, dedupe_key);",
                cancellationToken: ct);
            await db.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS ix_scrape_session_user_created ON public.scrape_session (user_id, created_at_utc);",
                cancellationToken: ct);

            _schemaEnsured = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private async Task<ProviderSearchResult> SearchProviderAsync(string provider, SourcedOfferSearchRequest request, CancellationToken ct)
    {
        try
        {
            return provider switch
            {
                "linkedin" => await SearchLinkedInAsync(request, ct),
                "indeed" => await SearchIndeedAsync(request, ct),
                "glassdoor" => await SearchGlassdoorAsync(request, ct),
                _ => new ProviderSearchResult
                {
                    Provider = provider,
                    Errors = [$"Unsupported provider '{provider}'."],
                },
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sourced offer search failed for provider {Provider}", provider);
            return new ProviderSearchResult
            {
                Provider = provider,
                Errors = [$"{provider}: {ex.Message}"],
            };
        }
    }

    private async Task<ProviderSearchResult> SearchLinkedInAsync(SourcedOfferSearchRequest request, CancellationToken ct)
    {
        var payload = new ProviderSearchPayload
        {
            Keywords = request.Keywords,
            Location = request.Location,
            Limit = request.Limit,
            PostedWindow = request.PostedWindow,
            FetchDetails = true,
            ItOnly = true,
            ContractTypes = request.ContractTypes,
        };
        var response = await agentHttpClient.PostAsync<ProviderSearchPayload, ProviderSearchResponse>("/linkedin-jobs/search", payload, ct);
        return BuildProviderResult("linkedin", response);
    }

    private async Task<ProviderSearchResult> SearchIndeedAsync(SourcedOfferSearchRequest request, CancellationToken ct)
    {
        var payload = new ProviderSearchPayload
        {
            Keywords = request.Keywords,
            Location = request.Location,
            Limit = request.Limit,
            PostedWindow = request.PostedWindow,
            FetchDetails = true,
            ItOnly = true,
            ContractTypes = request.ContractTypes,
            CountryCode = request.IndeedCountryCode,
        };
        var response = await agentHttpClient.PostAsync<ProviderSearchPayload, ProviderSearchResponse>("/indeed-jobs/search", payload, ct);
        return BuildProviderResult("indeed", response);
    }

    private async Task<ProviderSearchResult> SearchGlassdoorAsync(SourcedOfferSearchRequest request, CancellationToken ct)
    {
        var payload = new ProviderSearchPayload
        {
            Keywords = request.Keywords,
            Location = request.Location,
            Limit = request.Limit,
            PostedWindow = request.PostedWindow,
            FetchDetails = false,
            ItOnly = true,
            ContractTypes = request.ContractTypes,
        };
        var response = await agentHttpClient.PostAsync<ProviderSearchPayload, ProviderSearchResponse>("/glassdoor-jobs/search", payload, ct);
        return BuildProviderResult("glassdoor", response);
    }

    private ProviderSearchResult BuildProviderResult(string provider, ProviderSearchResponse response)
    {
        return new ProviderSearchResult
        {
            Provider = provider,
            Offers = (response.Jobs ?? [])
                .Select(job => NormalizeProviderJob(provider, job))
                .Where(job => job is not null)
                .Cast<NormalizedProviderOffer>()
                .ToList(),
            Warnings = response.Errors ?? [],
            Errors = [],
        };
    }

    private static NormalizedProviderOffer? NormalizeProviderJob(string provider, ProviderJobPayload job)
    {
        if (string.IsNullOrWhiteSpace(job.Title))
        {
            return null;
        }

        var postedWindow = InferPostedWindow(job.PostedAtText);
        var normalizedContractType = NormalizeContractType(job.NormalizedContractType, job.EmploymentType, job.Title, job.Description);
        return new NormalizedProviderOffer
        {
            Provider = provider,
            ProviderJobId = job.JobId,
            ExternalUrl = job.Url,
            Title = job.Title.Trim(),
            Company = NullIfWhiteSpace(job.Company),
            Location = NullIfWhiteSpace(job.Location),
            Description = NullIfWhiteSpace(job.Description),
            PostedAtText = NullIfWhiteSpace(job.PostedAtText),
            PostedWindow = postedWindow,
            RawContractType = NullIfWhiteSpace(job.EmploymentType),
            EmploymentType = NullIfWhiteSpace(job.EmploymentType),
            NormalizedContractType = normalizedContractType,
            SeniorityLevel = NullIfWhiteSpace(job.SeniorityLevel),
            MatchedItTerms = job.MatchedItTerms ?? [],
            DedupeKey = BuildDedupeKey(provider, job),
        };
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

    private static string BuildDedupeKey(string provider, ProviderJobPayload job)
    {
        var providerJobId = NullIfWhiteSpace(job.JobId);
        if (!string.IsNullOrWhiteSpace(providerJobId))
        {
            return $"{provider}:{providerJobId}".ToLowerInvariant();
        }

        return string.Join(
            "::",
            provider.ToLowerInvariant(),
            NormalizeToken(job.Title),
            NormalizeToken(job.Company),
            NormalizeToken(job.Location));
    }

    private static string NormalizeToken(string? value)
    {
        var clean = (value ?? string.Empty).Trim().ToLowerInvariant();
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ");
        return clean;
    }

    private static string InferPostedWindow(string? postedAtText)
    {
        var hours = ExtractRelativeHours(postedAtText);
        if (hours is null) return PostedWindowValues.Any;
        if (hours <= 24) return "24h";
        if (hours <= 24 * 3) return "3d";
        if (hours <= 24 * 7) return "7d";
        if (hours <= 24 * 14) return "14d";
        if (hours <= 24 * 30) return "30d";
        return PostedWindowValues.Any;
    }

    private static int? ExtractRelativeHours(string? postedAtText)
    {
        var clean = (postedAtText ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(clean)) return null;
        if (clean.Contains("today") || clean.Contains("just now") || clean.Contains("aujourd"))
        {
            return 0;
        }

        var match = System.Text.RegularExpressions.Regex.Match(clean, @"(\d+)");
        if (!match.Success) return null;
        var value = int.Parse(match.Groups[1].Value);
        if (clean.Contains("hour") || clean.Contains("hr") || clean.Contains("heure")) return value;
        if (clean.Contains("day") || clean.Contains("jour")) return value * 24;
        if (clean.Contains("week") || clean.Contains("semaine")) return value * 24 * 7;
        if (clean.Contains("month") || clean.Contains("mois")) return value * 24 * 30;
        return null;
    }

    private static string NormalizeContractType(string? normalizedContractType, string? employmentType, string? title, string? description)
    {
        if (!string.IsNullOrWhiteSpace(normalizedContractType))
        {
            return normalizedContractType.Trim().ToLowerInvariant();
        }

        var corpus = $"{employmentType} {title} {description}".ToLowerInvariant();
        var mappings = new Dictionary<string, string[]>
        {
            [NormalizedContractTypes.Internship] = ["internship", "intern", "stage", "stagiaire"],
            [NormalizedContractTypes.Cdi] = ["cdi", "permanent"],
            [NormalizedContractTypes.Cdd] = ["cdd", "fixed term", "fixed-term"],
            [NormalizedContractTypes.Freelance] = ["freelance", "freelancer", "contractor", "contract"],
            [NormalizedContractTypes.Alternance] = ["alternance", "apprenticeship", "apprenti"],
            [NormalizedContractTypes.PartTime] = ["part-time", "part time", "temps partiel"],
            [NormalizedContractTypes.FullTime] = ["full-time", "full time", "temps plein"],
            [NormalizedContractTypes.Temporary] = ["temporary", "temporaire", "interim"],
        };

        foreach (var entry in mappings)
        {
            if (entry.Value.Any(term => corpus.Contains(term)))
            {
                return entry.Key;
            }
        }

        return NormalizedContractTypes.Other;
    }

    private static string SerializeJson<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T DeserializeJson<T>(string? value, T fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        try
        {
            return JsonSerializer.Deserialize<T>(value, JsonOptions) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

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

    private static ScrapeSessionDto MapSession(ScrapeSession session) => new()
    {
        Id = session.Id,
        Keywords = session.Keywords,
        Location = session.Location,
        Providers = DeserializeJson(session.ProvidersJson, new List<string>()),
        CountryCode = session.CountryCode,
        PostedWindow = session.PostedWindow ?? PostedWindowValues.Any,
        ContractTypes = DeserializeJson(session.ContractTypesJson, new List<string>()),
        Limit = session.Limit,
        ResultCount = session.ResultCount,
        Warnings = DeserializeJson(session.WarningsJson, new List<string>()),
        Errors = DeserializeJson(session.ErrorsJson, new List<string>()),
        CreatedAtUtc = session.CreatedAtUtc,
    };

    private static SourcedOfferListItemDto MapListItem(SourcedOffer offer) => new()
    {
        Id = offer.Id,
        Provider = offer.Provider,
        ProviderJobId = offer.ProviderJobId,
        ExternalUrl = offer.ExternalUrl,
        Title = offer.Title,
        Company = offer.Company,
        Location = offer.Location,
        Description = offer.Description,
        PostedAtText = offer.PostedAtText,
        PostedWindow = offer.PostedWindow,
        RawContractType = offer.RawContractType,
        NormalizedContractType = offer.NormalizedContractType,
        EmploymentType = offer.EmploymentType,
        SeniorityLevel = offer.SeniorityLevel,
        MatchedItTerms = DeserializeJson(offer.MatchedItTermsJson, new List<string>()),
        IsSaved = offer.IsSaved,
        IsShortlisted = offer.IsShortlisted,
        IsArchived = offer.IsArchived,
        PromotedOfferId = offer.PromotedOfferId,
        FirstSeenAtUtc = offer.FirstSeenAtUtc,
        LastSeenAtUtc = offer.LastSeenAtUtc,
        ScrapedAtUtc = offer.ScrapedAtUtc,
    };

    private static SourcedOfferDetailDto MapDetail(SourcedOffer offer) => new()
    {
        Id = offer.Id,
        Provider = offer.Provider,
        ProviderJobId = offer.ProviderJobId,
        ExternalUrl = offer.ExternalUrl,
        Title = offer.Title,
        Company = offer.Company,
        Location = offer.Location,
        Description = offer.Description,
        PostedAtText = offer.PostedAtText,
        PostedWindow = offer.PostedWindow,
        RawContractType = offer.RawContractType,
        NormalizedContractType = offer.NormalizedContractType,
        EmploymentType = offer.EmploymentType,
        SeniorityLevel = offer.SeniorityLevel,
        MatchedItTerms = DeserializeJson(offer.MatchedItTermsJson, new List<string>()),
        IsSaved = offer.IsSaved,
        IsShortlisted = offer.IsShortlisted,
        IsArchived = offer.IsArchived,
        PromotedOfferId = offer.PromotedOfferId,
        FirstSeenAtUtc = offer.FirstSeenAtUtc,
        LastSeenAtUtc = offer.LastSeenAtUtc,
        ScrapedAtUtc = offer.ScrapedAtUtc,
        SourceQuery = DeserializeJson(offer.SourceQueryJson, new Dictionary<string, object?>()),
    };

    private sealed class ProviderSearchPayload
    {
        public string? Keywords { get; set; }
        public string? Location { get; set; }
        public int Limit { get; set; }
        public string? PostedWindow { get; set; }
        public bool FetchDetails { get; set; }
        public bool ItOnly { get; set; }
        public List<string> ContractTypes { get; set; } = new();
        public string? CountryCode { get; set; }
    }

    private sealed class ProviderSearchResponse
    {
        public int TotalFound { get; set; }
        public int TotalReturned { get; set; }
        public List<ProviderJobPayload> Jobs { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    private sealed class ProviderJobPayload
    {
        public string? JobId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Company { get; set; }
        public string? Location { get; set; }
        public string? PostedAtText { get; set; }
        public string? Url { get; set; }
        public string? Description { get; set; }
        public string? EmploymentType { get; set; }
        public string? NormalizedContractType { get; set; }
        public string? SeniorityLevel { get; set; }
        public List<string> MatchedItTerms { get; set; } = new();
    }

    private sealed class ProviderSearchResult
    {
        public string Provider { get; set; } = string.Empty;
        public List<NormalizedProviderOffer> Offers { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public string? Warning { get; set; }
    }

    private sealed class NormalizedProviderOffer
    {
        public string Provider { get; set; } = string.Empty;
        public string? ProviderJobId { get; set; }
        public string? ExternalUrl { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Company { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
        public string? PostedAtText { get; set; }
        public string? PostedWindow { get; set; }
        public string? RawContractType { get; set; }
        public string? NormalizedContractType { get; set; }
        public string? EmploymentType { get; set; }
        public string? SeniorityLevel { get; set; }
        public List<string> MatchedItTerms { get; set; } = new();
        public string DedupeKey { get; set; } = string.Empty;
    }
}
