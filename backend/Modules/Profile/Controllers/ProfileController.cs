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
using NextStep.Shared.Http;
using System.Net.Http.Json;

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
        private readonly IAgentHttpClient _agentHttpClient;
        private readonly IStorageService _storageService;

        public ProfileController(IProfileService profileService, IUserService userService, AppDbContext context, IAgentHttpClient agentHttpClient, IStorageService storageService)
        {
            _profileService = profileService;
            _userService = userService;
            _context = context;
            _agentHttpClient = agentHttpClient;
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

        [HttpGet("export")]
        public async Task<IActionResult> ExportProfileJson()
        {
            var userId = await GetUserIdAsync();
            var profile = await _profileService.GetFullProfileAsync(userId);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var jsonString = JsonSerializer.Serialize(profile, options);
            var bytes = Encoding.UTF8.GetBytes(jsonString);
            var prenom = profile.PersonalInfo?.Prenom ?? "user";
            var nom = profile.PersonalInfo?.Nom ?? "profile";
            var fileName = $"profile_{prenom}_{nom}_{DateTime.UtcNow:yyyyMMdd}.json";
            return File(bytes, "application/json", fileName);
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

        [HttpGet("photo")]
        public async Task<IActionResult> GetProfilePhoto()
        {
            var userId = await GetUserIdAsync();
            var user = await _context.Utilisateurs.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "Utilisateur non trouve." });

            var objectKey = ExtractObjectKey(user.PhotoUrl);
            if (string.IsNullOrWhiteSpace(objectKey))
                return NotFound(new { message = "Photo de profil introuvable." });

            var bytes = await _storageService.DownloadFileAsync(objectKey);
            return File(bytes, ResolveImageContentType(objectKey));
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

        private static string? ExtractObjectKey(string? storedUrl)
        {
            var rawUrl = (storedUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(rawUrl))
                return null;

            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed))
                return rawUrl.TrimStart('/');

            var path = parsed.AbsolutePath.Trim('/');
            var slashIdx = path.IndexOf('/');
            if (slashIdx <= 0 || slashIdx >= path.Length - 1)
                return null;

            return path[(slashIdx + 1)..];
        }

        private static string ResolveImageContentType(string objectKey)
        {
            var extension = Path.GetExtension(objectKey).ToLowerInvariant();
            return extension switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };
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
            // Dégradé 200 + erreurs explicites (décision produit) : le frontend
            // sait afficher un avertissement au lieu d'une fausse génération.
            try
            {
                // L'endpoint n'existe pas (encore) chez les agents Python.
                // On le signale comme service indisponible plutôt que d'inventer du contenu.
                var doc = await _agentHttpClient.PostRawAsync("/generate-resume", profileData);
                return Ok(new { resume = doc.RootElement.GetRawText() });
            }
            catch (Exception)
            {
                return Ok(new
                {
                    resume = string.Empty,
                    errors = new[]
                    {
                        "Le service de génération de CV par IA est actuellement indisponible."
                    }
                });
            }
        }

        [HttpPost("parse-resume")]
        public async Task<IActionResult> ParseResume(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Aucun fichier fourni." });

            try
            {
                using var memory = new MemoryStream();
                await file.CopyToAsync(memory);
                var json = await _agentHttpClient.PostFileAsync(
                    "/resume/parse", memory.ToArray(), file.FileName, file.ContentType);
                return Content(json, "application/json");
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new { error = ExtractReadableAgentError(ex), detail = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // L'erreur remontée par les agents contient déjà le message d'origine
        // du LLM (ex: modèle obsolète). On l'extrait pour un message lisible,
        // sinon on retombe sur un message générique stable pour l'UI.
        private static string ExtractReadableAgentError(HttpRequestException ex)
        {
            var msg = ex.Message;
            try
            {
                var prefixIndex = msg.IndexOf('{');
                if (prefixIndex >= 0)
                {
                    using var doc = JsonDocument.Parse(msg[prefixIndex..]);
                    if (doc.RootElement.TryGetProperty("detail", out var detail) &&
                        detail.ValueKind == JsonValueKind.String &&
                        detail.GetString() is { Length: > 0 } d)
                        return $"L'analyse de ce CV a échoué : {d}";
                    if (doc.RootElement.TryGetProperty("error", out var error) &&
                        error.ValueKind == JsonValueKind.String &&
                        error.GetString() is { Length: > 0 } e)
                        return $"L'analyse de ce CV a échoué : {e}";
                }
            }
            catch (JsonException)
            {
                // best-effort : on garde le message générique
            }
            return "Le service d'analyse de CV par IA n'a pas pu traiter ce fichier.";
        }

        [HttpPost("import-linkedin")]
        public async Task<IActionResult> ImportLinkedIn([FromBody] LinkedInImportDto dto)
        {
            if (dto == null || (string.IsNullOrWhiteSpace(dto.Url) && string.IsNullOrWhiteSpace(dto.RawText)))
                return BadRequest(new { error = "Aucune donnée fournie." });

            try
            {
                var doc = await _agentHttpClient.PostRawAsync("/resume/parse-linkedin", new { url = dto.Url, rawText = dto.RawText });
                return Content(doc.RootElement.GetRawText(), "application/json");
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, new { error = "Le service d'import LinkedIn est indisponible.", detail = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
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
