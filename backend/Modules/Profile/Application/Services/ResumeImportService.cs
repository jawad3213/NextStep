using System.Text.Json;
using System.Text.Json.Nodes;
using NextStep.Modules.Profile.Application.Dtos;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;

namespace NextStep.Modules.Profile.Application.Services;

public interface IResumeImportService
{
    Task<GenerateResumeResponse> GenerateResumeAsync(Guid userId, object profileData);
    Task<string> ParseResumeAsync(IFormFile? file);
    Task<string> ImportLinkedInAsync(LinkedInImportDto? dto);
}

/// <summary>CV / LinkedIn import and resume generation, delegated to the Python agents.</summary>
public class ResumeImportService(IAgentHttpClient agents, IProfileService profileService) : IResumeImportService
{
    public async Task<GenerateResumeResponse> GenerateResumeAsync(Guid userId, object profileData)
    {
        // Degraded 200 with explicit errors (product decision): the frontend shows a
        // warning and keeps the current summary instead of showing a fake generation.
        try
        {
            // The summary must come back in the user's preferred language regardless of what
            // the frontend's profile payload happens to contain, so it's stamped on server side.
            var language = await profileService.GetLanguagePreferenceAsync(userId);
            var payload = JsonNode.Parse(JsonSerializer.Serialize(profileData)) as JsonObject ?? [];
            payload["language"] = language;

            using var doc = await agents.PostRawAsync("/generate-resume", payload);
            var resume = doc.RootElement.ValueKind == JsonValueKind.Object
                         && doc.RootElement.TryGetProperty("resume", out var value)
                         && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;
            return string.IsNullOrEmpty(resume)
                ? new GenerateResumeResponse(string.Empty, ["The generated summary is empty. Please complete your profile and try again."])
                : new GenerateResumeResponse(resume);
        }
        catch (Exception)
        {
            return new GenerateResumeResponse(
                string.Empty,
                ["AI resume generation service is currently unavailable."]);
        }
    }

    /// <summary>Returns the parsed CV as the agents' raw JSON.</summary>
    public async Task<string> ParseResumeAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            throw new BadRequestException("No file provided.");

        try
        {
            using var memory = new MemoryStream();
            await file.CopyToAsync(memory);
            return await agents.PostFileAsync("/resume/parse", memory.ToArray(), file.FileName, file.ContentType);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamServiceException("Resume parsing service is unavailable.", ex);
        }
        catch (InvalidOperationException)
        {
            throw new BadRequestException("Invalid file or unsupported format.");
        }
    }

    /// <summary>Returns the imported LinkedIn profile as the agents' raw JSON.</summary>
    public async Task<string> ImportLinkedInAsync(LinkedInImportDto? dto)
    {
        if (dto == null || (string.IsNullOrWhiteSpace(dto.Url) && string.IsNullOrWhiteSpace(dto.RawText)))
            throw new BadRequestException("No data provided.");

        try
        {
            var doc = await agents.PostRawAsync("/resume/parse-linkedin", new { url = dto.Url, rawText = dto.RawText });
            return doc.RootElement.GetRawText();
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamServiceException("LinkedIn import service is unavailable.", ex);
        }
        catch (InvalidOperationException)
        {
            throw new BadRequestException("Invalid LinkedIn data.");
        }
    }
}
