using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.Modules.Profile.Application.Dtos;
using NextStep.Modules.Profile.Application.Services;
using NextStep.Modules.Profile.Application.Validation;
using NextStep.Shared.Api;

namespace NextStep.Modules.Profile.Api;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController(
    IProfileService profileService,
    IUserService userService,
    IProfilePhotoService photoService,
    IResumeImportService resumeImportService) : ControllerBase
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private async Task<Guid> GetUserIdAsync() => (await userService.EnsureUserCreatedAsync(User)).Id;

    [HttpGet]
    public async Task<ActionResult<FullProfileDto>> GetFullProfile()
    {
        var userId = await GetUserIdAsync();
        return Ok(await profileService.GetFullProfileAsync(userId));
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportProfileJson()
    {
        var userId = await GetUserIdAsync();
        var profile = await profileService.GetFullProfileAsync(userId);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(profile, ExportJsonOptions));
        var prenom = profile.PersonalInfo?.Prenom ?? "user";
        var nom = profile.PersonalInfo?.Nom ?? "profile";
        return File(bytes, "application/json", $"profile_{prenom}_{nom}_{DateTime.UtcNow:yyyyMMdd}.json");
    }

    [HttpPut("personal-info")]
    public async Task<ActionResult<MessageResponse>> UpdatePersonalInfo([FromBody] PersonalInfoDto dto)
    {
        await profileService.UpdatePersonalInfoAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Personal information updated successfully."));
    }

    /// <summary>UI and generated-document language ("en" or "fr"). Persisted server-side, not just in the browser.</summary>
    [HttpPut("language")]
    public async Task<ActionResult<MessageResponse>> UpdateLanguagePreference([FromBody] LanguagePreferenceDto dto)
    {
        await profileService.UpdateLanguagePreferenceAsync(await GetUserIdAsync(), dto.Language);
        return Ok(new MessageResponse("Language preference updated successfully."));
    }

    // ── Photo ───────────────────────────────────────────────────────────────

    [HttpPost("photo")]
    [RequestSizeLimit(ProfilePhotoValidator.MaxBytes)]
    public async Task<ActionResult<PhotoUploadResponse>> UploadProfilePhoto(IFormFile file) =>
        Ok(await photoService.UploadAsync(await GetUserIdAsync(), file));

    [HttpGet("photo")]
    public async Task<IActionResult> GetProfilePhoto()
    {
        var photo = await photoService.GetPhotoAsync(await GetUserIdAsync());
        return File(photo.Content, photo.ContentType);
    }

    [HttpGet("photo/signed")]
    public async Task<ActionResult<PhotoUrlResponse>> GetSignedProfilePhotoUrl() =>
        Ok(await photoService.GetSignedUrlAsync(await GetUserIdAsync()));

    // ── Sections ────────────────────────────────────────────────────────────

    [HttpPost("experiences")]
    public async Task<ActionResult<MessageResponse>> AddExperience([FromBody] ExperienceDto dto)
    {
        await profileService.AddExperienceAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Experience added successfully."));
    }

    [HttpPut("experiences")]
    public async Task<ActionResult<MessageResponse>> UpdateExperience([FromBody] ExperienceDto dto)
    {
        await profileService.UpdateExperienceAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Experience updated successfully."));
    }

    [HttpDelete("experiences/{id}")]
    public async Task<ActionResult<MessageResponse>> DeleteExperience(Guid id)
    {
        await profileService.DeleteExperienceAsync(await GetUserIdAsync(), id);
        return Ok(new MessageResponse("Experience deleted successfully."));
    }

    [HttpPost("projets")]
    public async Task<ActionResult<MessageResponse>> AddProjet([FromBody] ProjetDto dto)
    {
        await profileService.AddProjetAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Project added successfully."));
    }

    [HttpPut("projets")]
    public async Task<ActionResult<MessageResponse>> UpdateProjet([FromBody] ProjetDto dto)
    {
        await profileService.UpdateProjetAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Project updated successfully."));
    }

    [HttpDelete("projets/{id}")]
    public async Task<ActionResult<MessageResponse>> DeleteProjet(Guid id)
    {
        await profileService.DeleteProjetAsync(await GetUserIdAsync(), id);
        return Ok(new MessageResponse("Project deleted successfully."));
    }

    [HttpPost("competences")]
    public async Task<ActionResult<MessageResponse>> AddCompetence([FromBody] CompetenceDto dto)
    {
        await profileService.AddCompetenceAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Skill added successfully."));
    }

    [HttpPut("competences")]
    public async Task<ActionResult<MessageResponse>> UpdateCompetence([FromBody] CompetenceDto dto)
    {
        await profileService.UpdateCompetenceAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Skill updated successfully."));
    }

    [HttpDelete("competences/{id}")]
    public async Task<ActionResult<MessageResponse>> DeleteCompetence(Guid id)
    {
        await profileService.DeleteCompetenceAsync(await GetUserIdAsync(), id);
        return Ok(new MessageResponse("Skill deleted successfully."));
    }

    [HttpPost("formations")]
    public async Task<ActionResult<MessageResponse>> AddFormation([FromBody] FormationDto dto)
    {
        await profileService.AddFormationAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Education added successfully."));
    }

    [HttpPut("formations")]
    public async Task<ActionResult<MessageResponse>> UpdateFormation([FromBody] FormationDto dto)
    {
        await profileService.UpdateFormationAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Education updated successfully."));
    }

    [HttpDelete("formations/{id}")]
    public async Task<ActionResult<MessageResponse>> DeleteFormation(Guid id)
    {
        await profileService.DeleteFormationAsync(await GetUserIdAsync(), id);
        return Ok(new MessageResponse("Education deleted successfully."));
    }

    [HttpPost("certifications")]
    public async Task<ActionResult<MessageResponse>> AddCertification([FromBody] CertificationDto dto)
    {
        await profileService.AddCertificationAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Certification added successfully."));
    }

    [HttpPut("certifications")]
    public async Task<ActionResult<MessageResponse>> UpdateCertification([FromBody] CertificationDto dto)
    {
        await profileService.UpdateCertificationAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Certification updated successfully."));
    }

    [HttpDelete("certifications/{id}")]
    public async Task<ActionResult<MessageResponse>> DeleteCertification(Guid id)
    {
        await profileService.DeleteCertificationAsync(await GetUserIdAsync(), id);
        return Ok(new MessageResponse("Certification deleted successfully."));
    }

    [HttpPost("complete-onboarding")]
    public async Task<ActionResult<MessageResponse>> CompleteOnboarding([FromBody] OnboardingDto dto)
    {
        await profileService.CompleteOnboardingAsync(await GetUserIdAsync(), dto);
        return Ok(new MessageResponse("Onboarding completed successfully."));
    }

    [HttpDelete("clear")]
    public async Task<ActionResult<MessageResponse>> ClearProfile()
    {
        await profileService.ClearProfileAsync(await GetUserIdAsync());
        return Ok(new MessageResponse("Profile reset successfully."));
    }

    // ── Reference data & AI import ──────────────────────────────────────────

    [HttpGet("keywords")]
    [AllowAnonymous]
    public async Task<ActionResult<List<KeywordDto>>> GetKeywords() =>
        Ok(await profileService.GetKeywordsAsync());

    [HttpPost("generate-resume")]
    public async Task<ActionResult<GenerateResumeResponse>> GenerateResume([FromBody] object profileData) =>
        Ok(await resumeImportService.GenerateResumeAsync(await GetUserIdAsync(), profileData));

    [HttpPost("parse-resume")]
    public async Task<IActionResult> ParseResume(IFormFile file) =>
        Content(await resumeImportService.ParseResumeAsync(file), "application/json");

    [HttpPost("import-linkedin")]
    public async Task<IActionResult> ImportLinkedIn([FromBody] LinkedInImportDto dto) =>
        Content(await resumeImportService.ImportLinkedInAsync(dto), "application/json");
}
