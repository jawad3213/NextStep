namespace NextStep.Modules.Sourcing.DTOs;

public static class PostedWindowValues
{
    public const string Any = "any";
}

public static class SourcingProviders
{
    public const string Linkedin = "linkedin";
    public const string Indeed = "indeed";
    public const string Glassdoor = "glassdoor";

    public static readonly IReadOnlyList<string> All = [Linkedin, Indeed, Glassdoor];

    private static readonly HashSet<string> Allowed = new(All, StringComparer.OrdinalIgnoreCase);

    public static List<string> NormalizeRequestedProviders(IEnumerable<string>? providers)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawProvider in providers ?? [])
        {
            var provider = (rawProvider ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(provider))
            {
                continue;
            }

            if (!Allowed.Contains(provider))
            {
                throw new ArgumentException(
                    $"Unsupported sourcing provider '{provider}'. Allowed providers: {string.Join(", ", All)}.");
            }

            if (seen.Add(provider))
            {
                normalized.Add(provider);
            }
        }

        return normalized;
    }
}

public static class NormalizedContractTypes
{
    public const string Internship = "internship";
    public const string Cdi = "cdi";
    public const string Cdd = "cdd";
    public const string Freelance = "freelance";
    public const string Alternance = "alternance";
    public const string PartTime = "part_time";
    public const string FullTime = "full_time";
    public const string Temporary = "temporary";
    public const string Other = "other";
}

public static class SourcingContractFilters
{
    private static readonly Dictionary<string, string[]> Mappings = new()
    {
        [NormalizedContractTypes.Internship] = ["internship", "intern", "stage", "stagiaire", "stage pfe", "stage pre-embauche"],
        [NormalizedContractTypes.Cdi] = ["cdi", "permanent", "full-time permanent", "contrat a duree indeterminee"],
        [NormalizedContractTypes.Cdd] = ["cdd", "contrat a duree determinee", "fixed term", "fixed-term", "contractuel"],
        [NormalizedContractTypes.Freelance] = ["freelance", "freelancer", "contractor", "independent contractor", "consultant independant"],
        [NormalizedContractTypes.Alternance] = ["alternance", "apprenticeship", "apprenti", "work-study"],
        [NormalizedContractTypes.PartTime] = ["part-time", "part time", "temps partiel"],
        [NormalizedContractTypes.FullTime] = ["full-time", "full time", "temps plein"],
        [NormalizedContractTypes.Temporary] = ["temporary", "temporaire", "interim"],
    };

    private static readonly HashSet<string> KnownTypes = new(
        Mappings.Keys.Append(NormalizedContractTypes.Other),
        StringComparer.OrdinalIgnoreCase);

    public static bool Matches(
        IEnumerable<string>? requestedContractTypes,
        string? normalizedContractType,
        string? employmentType,
        string? title,
        string? description,
        string? rawContractType = null,
        string? seniorityLevel = null,
        string? jobFunction = null)
    {
        var requested = (requestedContractTypes ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (requested.Count == 0)
        {
            return true;
        }

        var actual = NormalizeContractType(
            normalizedContractType,
            employmentType,
            title,
            description,
            rawContractType,
            seniorityLevel,
            jobFunction);

        return !string.IsNullOrWhiteSpace(actual) && requested.Contains(actual);
    }

    public static string NormalizeContractType(
        string? normalizedContractType,
        string? employmentType,
        string? title,
        string? description,
        string? rawContractType = null,
        string? seniorityLevel = null,
        string? jobFunction = null)
    {
        var explicitType = normalizedContractType?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(explicitType) &&
            KnownTypes.Contains(explicitType) &&
            !string.Equals(explicitType, NormalizedContractTypes.Other, StringComparison.OrdinalIgnoreCase))
        {
            return explicitType;
        }

        var primaryCorpus = $"{employmentType} {rawContractType} {title} {jobFunction} {seniorityLevel}".ToLowerInvariant();
        var secondaryCorpus = (description ?? string.Empty).ToLowerInvariant();

        foreach (var corpus in new[] { primaryCorpus, secondaryCorpus })
        {
            if (string.IsNullOrWhiteSpace(corpus))
            {
                continue;
            }

            foreach (var entry in Mappings)
            {
                if (entry.Value.Any(term => ContainsContractTerm(corpus, term)))
                {
                    return entry.Key;
                }
            }
        }

        return NormalizedContractTypes.Other;
    }

    private static bool ContainsContractTerm(string corpus, string term)
    {
        var escaped = System.Text.RegularExpressions.Regex.Escape(term.ToLowerInvariant())
            .Replace("\\ ", "\\s+");
        return System.Text.RegularExpressions.Regex.IsMatch(
            corpus.ToLowerInvariant(),
            $@"(?<![a-z0-9]){escaped}(?![a-z0-9])");
    }
}

public class SourcedOfferSearchRequest
{
    public string? Keywords { get; set; }
    public string? Location { get; set; }
    public List<string> Providers { get; set; } = new();
    public int Limit { get; set; } = 12;
    public string PostedWindow { get; set; } = PostedWindowValues.Any;
    public List<string> ContractTypes { get; set; } = new();
    public string? IndeedCountryCode { get; set; } = "ma";
    public string? WorkflowState { get; set; }
}

public class SourcedOfferUpdateRequest
{
    public bool? IsSaved { get; set; }
    public bool? IsShortlisted { get; set; }
    public bool? IsArchived { get; set; }
}

public class ScrapeSessionDto
{
    public Guid Id { get; set; }
    public string? Keywords { get; set; }
    public string? Location { get; set; }
    public List<string> Providers { get; set; } = new();
    public string? CountryCode { get; set; }
    public string PostedWindow { get; set; } = PostedWindowValues.Any;
    public List<string> ContractTypes { get; set; } = new();
    public int Limit { get; set; }
    public int ResultCount { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
}

public class SourcedOfferListItemDto
{
    public Guid Id { get; set; }
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
    public int? AiScore { get; set; }
    public int? AiScoreSkills { get; set; }
    public int? AiScoreTitle { get; set; }
    public int? AiScoreLocation { get; set; }
    public int? AiScoreContract { get; set; }
    public int? AiScoreFreshness { get; set; }
    public double? AiConfidence { get; set; }
    public List<string> AiMatchedSkills { get; set; } = new();
    public List<string> AiMissingSkills { get; set; } = new();
    public List<string> AiReasons { get; set; } = new();
    public string? AiSummary { get; set; }
    public DateTime? AiRankedAtUtc { get; set; }
    public bool IsSaved { get; set; }
    public bool IsShortlisted { get; set; }
    public bool IsArchived { get; set; }
    public Guid? PromotedOfferId { get; set; }
    public DateTime FirstSeenAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime ScrapedAtUtc { get; set; }
}

public class SourcedOfferDetailDto : SourcedOfferListItemDto
{
    public Dictionary<string, object?> SourceQuery { get; set; } = new();
    public List<SourcedOfferListItemDto> SimilarOffers { get; set; } = new();
}

public class PromoteSourcedOfferResponse
{
    public Guid OfferId { get; set; }
    public bool AlreadyPromoted { get; set; }
}

public class SourcedOfferSearchResponse
{
    public ScrapeSessionDto? Session { get; set; }
    public List<SourcedOfferListItemDto> Offers { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
