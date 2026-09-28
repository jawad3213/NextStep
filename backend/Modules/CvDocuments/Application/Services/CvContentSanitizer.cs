using NextStep.Modules.CvDocuments.Infrastructure.Rendering;
using System.Text.Json;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.CvDocuments.Domain;

namespace NextStep.Modules.CvDocuments.Application.Services;

/// <summary>CV Documents' implementation of the Applications port <see cref="ICvContentSanitizer"/>.</summary>
public class CvContentSanitizer : ICvContentSanitizer
{
    private static readonly JsonSerializerOptions CvJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string Sanitize(JsonElement cvJson)
    {
        var payload = cvJson.ValueKind == JsonValueKind.Object && cvJson.TryGetProperty("data", out var data)
            ? data
            : cvJson;
        var cvData = JsonSerializer.Deserialize<CvData>(payload.GetRawText(), CvJsonOptions) ?? new CvData();
        return JsonSerializer.Serialize(CvDataSanitizer.Sanitize(cvData), CvJsonOptions);
    }
}
