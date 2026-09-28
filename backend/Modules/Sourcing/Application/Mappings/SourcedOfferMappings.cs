using System.Text.Json;
using NextStep.Modules.Sourcing.Application.Dtos;
using NextStep.Modules.Sourcing.Domain;

namespace NextStep.Modules.Sourcing.Application.Mappings;

public static class SourcedOfferMappings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static ScrapeSessionDto ToDto(this ScrapeSession session) => new()
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

    public static SourcedOfferListItemDto ToListItemDto(this SourcedOffer offer) => new()
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

    public static SourcedOfferDetailDto ToDetailDto(this SourcedOffer offer) => new()
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
}
