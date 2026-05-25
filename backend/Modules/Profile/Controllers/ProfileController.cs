using System.Security.Claims;
using NextStep.Modules.Profile.DTOs;
using NextStep.Modules.Profile.Services;
using NextStep.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextStep.data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;
using NextStep.Shared.Storage;

namespace NextStep.Modules.Profile.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;
        private readonly IUserService _userService;
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IStorageService _storageService;

        public ProfileController(IProfileService profileService, IUserService userService, AppDbContext context, IHttpClientFactory httpClientFactory, IStorageService storageService)
        {
            _profileService = profileService;
            _userService = userService;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _storageService = storageService;
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

        [HttpPost("photo")]
        [RequestSizeLimit(2 * 1024 * 1024)]
        public async Task<IActionResult> UploadProfilePhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Aucune image fournie." });

            if (file.Length > 2 * 1024 * 1024)
                return BadRequest(new { message = "L'image ne doit pas depasser 2MB." });

            var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/png",
                "image/jpeg",
                "image/jpg",
                "image/webp"
            };

            if (!allowedTypes.Contains(file.ContentType))
                return BadRequest(new { message = "Format d'image non supporte. Utilisez PNG, JPG ou WEBP." });

            var userId = await GetUserIdAsync();
            var user = await _context.Utilisateurs.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "Utilisateur non trouve." });

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png"
                    : file.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) ? ".webp"
                    : ".jpg";
            }

            var objectKey = $"profiles/{userId}/avatar-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";

            await using var stream = file.OpenReadStream();
            await using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            var photoUrl = await _storageService.UploadFileAsync(objectKey, memory.ToArray(), file.ContentType);
            user.PhotoUrl = photoUrl;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                photoUrl,
                objectKey,
                message = "Photo de profil televersee avec succes."
            });
        }

        [HttpGet("photo/signed")]
        public async Task<IActionResult> GetSignedProfilePhotoUrl()
        {
            var userId = await GetUserIdAsync();
            var user = await _context.Utilisateurs.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "Utilisateur non trouve." });

            var rawUrl = (user.PhotoUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawUrl))
                return Ok(new { photoUrl = (string?)null });

            // If this is not a MinIO/S3 URL we just return it as-is.
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed))
                return Ok(new { photoUrl = rawUrl });

            var path = parsed.AbsolutePath.Trim('/');
            var slashIdx = path.IndexOf('/');
            if (slashIdx <= 0 || slashIdx >= path.Length - 1)
                return Ok(new { photoUrl = rawUrl });

            // URL shape: /{bucket}/{objectKey}
            var objectKey = path[(slashIdx + 1)..];
            if (string.IsNullOrWhiteSpace(objectKey))
                return Ok(new { photoUrl = rawUrl });

            try
            {
                var signedUrl = await _storageService.GetPresignedUrlAsync(objectKey, TimeSpan.FromHours(6));
                return Ok(new { photoUrl = signedUrl, objectKey });
            }
            catch
            {
                // Fallback to stored URL if signing fails
                return Ok(new { photoUrl = rawUrl });
            }
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

        // Certifications
        [HttpPost("certifications")]
        public async Task<IActionResult> AddCertification([FromBody] CertificationDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.AddCertificationAsync(userId, dto);
            return Ok(new { message = "Certification ajoutée." });
        }

        [HttpPut("certifications")]
        public async Task<IActionResult> UpdateCertification([FromBody] CertificationDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.UpdateCertificationAsync(userId, dto);
            return Ok(new { message = "Certification mise à jour." });
        }

        [HttpDelete("certifications/{id}")]
        public async Task<IActionResult> DeleteCertification(Guid id)
        {
            var userId = await GetUserIdAsync();
            await _profileService.DeleteCertificationAsync(userId, id);
            return Ok(new { message = "Certification supprimée." });
        }

        [HttpPost("complete-onboarding")]
        public async Task<IActionResult> CompleteOnboarding([FromBody] OnboardingDto dto)
        {
            var userId = await GetUserIdAsync();
            await _profileService.CompleteOnboardingAsync(userId, dto);
            return Ok(new { message = "Onboarding terminé avec succès." });
        }

        // --- Nouveaux endpoints pour retirer les données "en dur" du frontend ---

        [HttpGet("keywords")]
        [AllowAnonymous]
        public async Task<IActionResult> GetKeywords()
        {
            // Récupère les mots clés depuis la BD au lieu du hardcode frontend
            var keywords = await _context.Keywords.Select(k => new { k.Mot, k.Categorie }).ToListAsync();
            return Ok(keywords);
        }

        [HttpPost("generate-resume")]
        public async Task<IActionResult> GenerateResume([FromBody] object profileData)
        {
            try 
            {
                // Proxy vers le conteneur Python Agent
                var client = _httpClientFactory.CreateClient();
                var agentUrl = Environment.GetEnvironmentVariable("PythonAgents__Url") ?? "http://agents-python:8000";
                
                var content = new StringContent(JsonSerializer.Serialize(profileData), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{agentUrl}/generate-resume", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return Ok(new { resume = result });
                }
                
                // Fallback si l'IA n'est pas prête
                return Ok(new { resume = "Expert passionné avec une solide expérience technique. Toujours à la recherche de nouveaux défis pour innover et apporter de la valeur." });
            }
            catch (Exception)
            {
                return Ok(new { resume = "Expert passionné avec une solide expérience technique. Toujours à la recherche de nouveaux défis pour innover et apporter de la valeur." });
            }
        }

        [HttpPost("parse-resume")]
        public async Task<IActionResult> ParseResume(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Aucun fichier fourni.");

            try 
            {
                var client = _httpClientFactory.CreateClient();
                var agentUrl = Environment.GetEnvironmentVariable("PythonAgents__Url") ?? "http://agents-python:8000";
                
                using var content = new MultipartFormDataContent();
                using var stream = file.OpenReadStream();
                content.Add(new StreamContent(stream), "file", file.FileName);
                
                var response = await client.PostAsync($"{agentUrl}/resume/parse", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return Content(result, "application/json"); 
                }
                
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, $"Erreur agent IA : {error}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur proxy IA : {ex.Message}");
            }
        }

        [HttpPost("import-linkedin")]
        public async Task<IActionResult> ImportLinkedIn([FromBody] LinkedInImportDto dto)
        {
            if (dto == null)
                return BadRequest("Aucune donnée fournie.");

            try 
            {
                var client = _httpClientFactory.CreateClient();
                var agentUrl = Environment.GetEnvironmentVariable("PythonAgents__Url") ?? "http://agents-python:8000";
                
                var jsonContent = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{agentUrl}/resume/parse-linkedin", jsonContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return Content(result, "application/json"); 
                }
                
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, $"Erreur agent IA : {error}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erreur proxy IA : {ex.Message}");
            }
        }
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearProfile()
        {
            var userId = await GetUserIdAsync();
            
            // Delete all related records for this user
            var experiences = _context.Experiences.Where(e => e.UserId == userId);
            var formations = _context.Formations.Where(f => f.UserId == userId);
            var competences = _context.Competences.Where(c => c.UserId == userId);
            var projets = _context.Projets.Where(p => p.UserId == userId);
            var certifications = _context.Certifications.Where(c => c.UserId == userId);

            _context.Experiences.RemoveRange(experiences);
            _context.Formations.RemoveRange(formations);
            _context.Competences.RemoveRange(competences);
            _context.Projets.RemoveRange(projets);
            _context.Certifications.RemoveRange(certifications);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Profil réinitialisé avec succès." });
        }
    }
}
