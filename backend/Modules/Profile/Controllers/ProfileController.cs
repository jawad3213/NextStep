using System.Security.Claims;
using NextStep.Modules.Profile.DTOs;
using NextStep.Modules.Profile.Services;
using NextStep.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NextStep.Modules.Profile.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly IUserService _userService;

        public ProfileController(IProfileService profileService, IUserService userService)
        {
            _profileService = profileService;
            _userService = userService;
        }

        private async Task<Guid> GetUserIdAsync()
        {
            var keycloakId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst("sub")?.Value;
            var user = await _userService.EnsureUserCreatedAsync(User);
            return user.Id;
        }

        [HttpGet]
        public async Task<IActionResult> GetFullProfile()
        {
            var userId = await GetUserIdAsync();
            var profile = await _profileService.GetFullProfileAsync(userId);
            return Ok(profile);
        }

        [HttpPut("personal-info")]
        public async Task<IActionResult> UpdatePersonalInfo([FromBody] PersonalInfoDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.UpdatePersonalInfoAsync(userId, dto);
            return Ok(new { message = "Infos personnelles mises à jour." });
        }

        // Experiences
        [HttpPost("experiences")]
        public async Task<IActionResult> AddExperience([FromBody] ExperienceDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.AddExperienceAsync(userId, dto);
            return Ok(new { message = "Expérience ajoutée." });
        }

        [HttpPut("experiences")]
        public async Task<IActionResult> UpdateExperience([FromBody] ExperienceDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.UpdateExperienceAsync(userId, dto);
            return Ok(new { message = "Expérience mise à jour." });
        }

        [HttpDelete("experiences/{id}")]
        public async Task<IActionResult> DeleteExperience(Guid id)
        {
            var userId = await GetUserIdAsync();
            await _profileService.DeleteExperienceAsync(userId, id);
            return Ok(new { message = "Expérience supprimée." });
        }

        // Projets
        [HttpPost("projets")]
        public async Task<IActionResult> AddProjet([FromBody] ProjetDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.AddProjetAsync(userId, dto);
            return Ok(new { message = "Projet ajouté." });
        }

        [HttpPut("projets")]
        public async Task<IActionResult> UpdateProjet([FromBody] ProjetDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.UpdateProjetAsync(userId, dto);
            return Ok(new { message = "Projet mis à jour." });
        }

        [HttpDelete("projets/{id}")]
        public async Task<IActionResult> DeleteProjet(Guid id)
        {
            var userId = await GetUserIdAsync();
            await _profileService.DeleteProjetAsync(userId, id);
            return Ok(new { message = "Projet supprimé." });
        }

        // Competences
        [HttpPost("competences")]
        public async Task<IActionResult> AddCompetence([FromBody] CompetenceDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.AddCompetenceAsync(userId, dto);
            return Ok(new { message = "Compétence ajoutée." });
        }

        [HttpPut("competences")]
        public async Task<IActionResult> UpdateCompetence([FromBody] CompetenceDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.UpdateCompetenceAsync(userId, dto);
            return Ok(new { message = "Compétence mise à jour." });
        }

        [HttpDelete("competences/{id}")]
        public async Task<IActionResult> DeleteCompetence(Guid id)
        {
            var userId = await GetUserIdAsync();
            await _profileService.DeleteCompetenceAsync(userId, id);
            return Ok(new { message = "Compétence supprimée." });
        }

        // Formations
        [HttpPost("formations")]
        public async Task<IActionResult> AddFormation([FromBody] FormationDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.AddFormationAsync(userId, dto);
            return Ok(new { message = "Formation ajoutée." });
        }

        [HttpPut("formations")]
        public async Task<IActionResult> UpdateFormation([FromBody] FormationDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.UpdateFormationAsync(userId, dto);
            return Ok(new { message = "Formation mise à jour." });
        }

        [HttpDelete("formations/{id}")]
        public async Task<IActionResult> DeleteFormation(Guid id)
        {
            var userId = await GetUserIdAsync();
            await _profileService.DeleteFormationAsync(userId, id);
            return Ok(new { message = "Formation supprimée." });
        }

        [HttpPost("complete-onboarding")]
        public async Task<IActionResult> CompleteOnboarding([FromBody] OnboardingDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.CompleteOnboardingAsync(userId, dto);
            return Ok(new { message = "Onboarding terminé avec succès." });
        }
    }
}
