namespace NextStep.Modules.Sourcing.DTOs;

public static class PostedWindowValues
{
    public const string Any = "any";
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
