using NextStep.Modules.Sourcing.Application.Services;
using System.Text.Json;
using System.Text.RegularExpressions;
using NextStep.Modules.Sourcing.Application.Dtos;
using NextStep.Shared.Http;

namespace NextStep.Modules.Sourcing.Infrastructure.JobBoards;

/// <summary>
/// Searches the job boards (LinkedIn, Indeed, Glassdoor) through the Python agents and
/// normalises their jobs (contract type, posted window, dedupe key). No persistence.
/// </summary>
public class JobProviderClient(IAgentHttpClient agentHttpClient, ILogger<JobProviderClient> logger)
{
    public async Task<ProviderSearchResult> SearchAsync(string provider, SourcedOfferSearchRequest request, CancellationToken ct)
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
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // The exception text stays in the logs: it can hold internal URLs and is no use to the user.
            logger.LogWarning(ex, "Sourced offer search failed for provider {Provider}", provider);
            return new ProviderSearchResult
            {
                Provider = provider,
                Errors = [$"{DisplayName(provider)} is unavailable right now. Showing cached offers for this provider."],
            };
        }
    }

    private static string DisplayName(string provider) => provider switch
    {
        "linkedin" => "LinkedIn",
        "indeed" => "Indeed",
        "glassdoor" => "Glassdoor",
        _ => provider,
    };

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

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
}

public sealed class ProviderSearchResult
{
    public string Provider { get; set; } = string.Empty;
    public List<NormalizedProviderOffer> Offers { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public string? Warning { get; set; }
}

public sealed class NormalizedProviderOffer
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
