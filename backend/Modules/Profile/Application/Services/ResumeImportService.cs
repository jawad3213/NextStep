using NextStep.Modules.Profile.Application.Dtos;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;

namespace NextStep.Modules.Profile.Application.Services;

public interface IResumeImportService
{
    Task<GenerateResumeResponse> GenerateResumeAsync(object profileData);
    Task<string> ParseResumeAsync(IFormFile? file);
    Task<string> ImportLinkedInAsync(LinkedInImportDto? dto);
}

/// <summary>CV / LinkedIn import and resume generation, delegated to the Python agents.</summary>
public class ResumeImportService(IAgentHttpClient agents) : IResumeImportService
{
    public async Task<GenerateResumeResponse> GenerateResumeAsync(object profileData)
    {
        // Degraded 200 with explicit errors (product decision): the frontend shows a
        // warning instead of a fake generation. The agents do not expose this endpoint yet.
        try
        {
            var doc = await agents.PostRawAsync("/generate-resume", profileData);
            return new GenerateResumeResponse(doc.RootElement.GetRawText());
        }
        catch (Exception)
        {
            return new GenerateResumeResponse(
                string.Empty,
                ["Le service de génération de CV par IA est actuellement indisponible."]);
        }
    }

    /// <summary>Returns the parsed CV as the agents' raw JSON.</summary>
    public async Task<string> ParseResumeAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            throw new BadRequestException("Aucun fichier fourni.");

        try
        {
            using var memory = new MemoryStream();
            await file.CopyToAsync(memory);
            return await agents.PostFileAsync("/resume/parse", memory.ToArray(), file.FileName, file.ContentType);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamServiceException("Le service d'analyse de CV est indisponible.", ex);
        }
        catch (InvalidOperationException)
        {
            throw new BadRequestException("Fichier invalide ou format non supporte.");
        }
    }

    /// <summary>Returns the imported LinkedIn profile as the agents' raw JSON.</summary>
    public async Task<string> ImportLinkedInAsync(LinkedInImportDto? dto)
    {
        if (dto == null || (string.IsNullOrWhiteSpace(dto.Url) && string.IsNullOrWhiteSpace(dto.RawText)))
            throw new BadRequestException("Aucune donnée fournie.");

        try
        {
            var doc = await agents.PostRawAsync("/resume/parse-linkedin", new { url = dto.Url, rawText = dto.RawText });
            return doc.RootElement.GetRawText();
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamServiceException("Le service d'import LinkedIn est indisponible.", ex);
        }
        catch (InvalidOperationException)
        {
            throw new BadRequestException("Donnees LinkedIn invalides.");
        }
    }
}
