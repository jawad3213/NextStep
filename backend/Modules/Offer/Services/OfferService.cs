using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NextStep.Modules.Offer.DTOs;
using NextStep.Modules.Offer.Models;
using NextStep.Modules.Offer.Repositories;
using NextStep.data;
using NextStep.Modules.Candidature.Models;
using NextStep.Modules.Cv.Models;
using NextStep.Modules.Cv.Services;

namespace NextStep.Modules.Offer.Services;

public interface IOfferService
{
    Task<OffreEmploi> SaveOfferAsync(string rawText, string userId, CancellationToken ct = default);
    Task<OfferAnalysisDto?> GetAnalysisAsync(Guid offerId, CancellationToken ct = default);
    Task<List<OfferHistoryItemDto>> GetHistoryAsync(Guid userId, CancellationToken ct = default);
    Task<int> DeleteOffersAsync(Guid userId, List<Guid> offerIds, CancellationToken ct = default);
    Task SavePipelineResultAsync(Guid offerId, JsonDocument pipelineResult, Guid userId, CancellationToken ct = default);
    Task<OffreEmploi?> GetOfferWithAnalysisAsync(Guid offerId, CancellationToken ct = default);
    Task<CvDraftDto?> GetCvDraftAsync(Guid userId, Guid offerId, CancellationToken ct = default);
    Task<CvDraftDto> SaveCvDraftAsync(Guid userId, Guid offerId, JsonElement draft, CancellationToken ct = default);
}

public class CvDraftDto
{
    public Guid OfferId { get; set; }
    public object? Data { get; set; }
    public int Version { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class OfferService(
    IOfferRepository repository,
    AppDbContext db,
    ILogger<OfferService> logger) : IOfferService
{
    private static readonly JsonSerializerOptions CvJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async Task<OffreEmploi> SaveOfferAsync(string rawText, string userId, CancellationToken ct = default)
    {
        Guid userGuid = Guid.TryParse(userId, out var parsedGuid) ? parsedGuid : Guid.Empty;

        var offre = new OffreEmploi
        {
            TexteBrut = rawText,
            UtilisateurId = userGuid,
        };
        offre = await repository.SaveAsync(offre, ct);
        logger.LogInformation("OfferService — Offre sauvegardée {OfferId}", offre.Id);
        return offre;
    }

    public async Task SavePipelineResultAsync(Guid offerId, JsonDocument pipelineResult, Guid userId, CancellationToken ct = default)
    {
        var root = pipelineResult.RootElement;
        var fullJson = root.GetRawText();
        var offer = await db.OffresEmploi.FirstOrDefaultAsync(o => o.Id == offerId, ct);

        if (offer is null)
        {
            throw new InvalidOperationException($"Offer {offerId} not found before pipeline persistence.");
        }

        // Save the latest pipeline payload first so the frontend can reload the
        // fresh analysis/CV output even if draft persistence fails later on.
        offer.AnalyseJson = fullJson;
        await db.SaveChangesAsync(ct);

        var cvDataJson = root.TryGetProperty("cv_data", out var cd)
            ? NormalizeDraftJson(cd)
            : null;

        // Analysis-only runs should only persist the offer analysis.
        // We create candidature/documents once the generation phase returns cv_data.
        if (cvDataJson != null)
        {
            try
            {
                var candidature = await db.Candidatures
                    .FirstOrDefaultAsync(c =>
                        c.IdOffre == offerId &&
                        c.IdUtilisateur == userId,
                        ct);

                if (candidature == null)
                {
                    candidature = new NextStep.Modules.Candidature.Models.Candidature
                    {
                        IdUtilisateur = userId,
                        IdOffre = offerId,
                        Offre = offer,
                        Statut = "EN_ATTENTE",
                        DateCreation = DateTime.UtcNow
                    };
                    db.Candidatures.Add(candidature);
                    await db.SaveChangesAsync(ct);
                }

                var existingDocument = await db.DocumentsGeneres
                    .FirstOrDefaultAsync(d => d.IdCandidature == candidature.IdCandidature, ct);

                if (existingDocument != null)
                {
                    existingDocument.CvContenuIaJson = cvDataJson;
                    existingDocument.Version += 1;
                    existingDocument.DateGeneration = DateTime.UtcNow;
                }
                else
                {
                    var documentGenere = new DocumentGenere
                    {
                        IdCandidature = candidature.IdCandidature,
                        CvContenuIaJson = cvDataJson,
                        Version = 1,
                        DateGeneration = DateTime.UtcNow
                    };
                    db.DocumentsGeneres.Add(documentGenere);
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "OfferService — CV draft persistence failed for offer {OfferId}. analyse_json was saved and will still be returned to the frontend.",
                    offerId);
            }
        }

        logger.LogInformation("OfferService — Résultats pipeline sauvegardés pour offre {OfferId}", offerId);
    }

    public async Task<OfferAnalysisDto?> GetAnalysisAsync(Guid offerId, CancellationToken ct = default)
    {
        var offre = await repository.GetByIdAsync(offerId, ct);
        if (offre is null) return null;
        if (string.IsNullOrEmpty(offre.AnalyseJson)) return null;

        var doc = JsonDocument.Parse(offre.AnalyseJson);
        return MapToDto(offre.Id, doc.RootElement, offre.TexteBrut);
    }

    public async Task<CvDraftDto?> GetCvDraftAsync(Guid userId, Guid offerId, CancellationToken ct = default)
    {
        await EnsureOfferOwnedAsync(userId, offerId, ct);

        var document = await (
            from c in db.Candidatures
            join d in db.DocumentsGeneres on c.IdCandidature equals d.IdCandidature
            where c.IdOffre == offerId && c.IdUtilisateur == userId
            select d
        ).FirstOrDefaultAsync(ct);

        if (document?.CvContenuIaJson is null) return null;

        return new CvDraftDto
        {
            OfferId = offerId,
            Data = JsonSerializer.Deserialize<object>(document.CvContenuIaJson),
            Version = document.Version,
            UpdatedAtUtc = document.DateGeneration,
        };
    }

    public async Task<CvDraftDto> SaveCvDraftAsync(Guid userId, Guid offerId, JsonElement draft, CancellationToken ct = default)
    {
        await EnsureOfferOwnedAsync(userId, offerId, ct);

        var sanitized = NormalizeDraftJson(draft);
        var offer = await db.OffresEmploi.FirstOrDefaultAsync(o => o.Id == offerId, ct);
        if (offer is null)
        {
            throw new KeyNotFoundException($"Offer {offerId} not found.");
        }

        var candidature = await db.Candidatures
            .FirstOrDefaultAsync(c => c.IdOffre == offerId && c.IdUtilisateur == userId, ct);

        if (candidature == null)
        {
            candidature = new NextStep.Modules.Candidature.Models.Candidature
            {
                IdUtilisateur = userId,
                IdOffre = offerId,
                Offre = offer,
                Statut = "EN_ATTENTE",
                DateCreation = DateTime.UtcNow
            };
            db.Candidatures.Add(candidature);
            await db.SaveChangesAsync(ct);
        }

        var document = await db.DocumentsGeneres
            .FirstOrDefaultAsync(d => d.IdCandidature == candidature.IdCandidature, ct);

        var now = DateTime.UtcNow;
        if (document == null)
        {
            document = new DocumentGenere
            {
                IdCandidature = candidature.IdCandidature,
                CvContenuIaJson = sanitized,
                Version = 1,
                DateGeneration = now,
            };
            db.DocumentsGeneres.Add(document);
        }
        else
        {
            document.CvContenuIaJson = sanitized;
            document.Version += 1;
            document.DateGeneration = now;
        }

        await db.SaveChangesAsync(ct);

        return new CvDraftDto
        {
            OfferId = offerId,
            Data = JsonSerializer.Deserialize<object>(sanitized),
            Version = document.Version,
            UpdatedAtUtc = document.DateGeneration,
        };
    }

    public async Task<List<OfferHistoryItemDto>> GetHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        var offers = await db.OffresEmploi
            .Where(o => o.UtilisateurId == userId)
            .OrderByDescending(o => o.DateCreation)
            .ToListAsync(ct);

        var offerIds = offers.Select(o => o.Id).ToList();
        var generatedOfferIds = await (
            from c in db.Candidatures
            join d in db.DocumentsGeneres on c.IdCandidature equals d.IdCandidature
            where offerIds.Contains(c.IdOffre)
            select c.IdOffre
        ).Distinct().ToListAsync(ct);
        var generatedSet = generatedOfferIds.ToHashSet();

        var list = new List<OfferHistoryItemDto>(offers.Count);
        foreach (var offer in offers)
        {
            string title = "Offre";
            string company = "";
            string location = "";
            int? score = null;
            var status = "non_traitee";
            var currentStep = 1;

            if (!string.IsNullOrWhiteSpace(offer.AnalyseJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(offer.AnalyseJson);
                    var dto = MapToDto(offer.Id, doc.RootElement, offer.TexteBrut);
                    title = string.IsNullOrWhiteSpace(dto.Titre) ? title : dto.Titre;
                    company = dto.Entreprise ?? "";
                    location = dto.Localisation ?? "";
                    score = dto.ScoreMatching > 0 ? dto.ScoreMatching : null;
                    status = "analysee";
                    currentStep = 2;
                }
                catch
                {
                    status = "analysee";
                    currentStep = 2;
                }
            }

            if (generatedSet.Contains(offer.Id))
            {
                status = "cv_genere";
                currentStep = 5;
            }

            list.Add(new OfferHistoryItemDto
            {
                OfferId = offer.Id,
                Titre = title,
                Entreprise = company,
                Localisation = location,
                ScoreMatching = score,
                Status = status,
                CurrentStep = currentStep,
                DateCreation = offer.DateCreation,
            });
        }

        return list;
    }

    public async Task<int> DeleteOffersAsync(Guid userId, List<Guid> offerIds, CancellationToken ct = default)
    {
        if (offerIds.Count == 0) return 0;

        var offers = await db.OffresEmploi
            .Where(o => o.UtilisateurId == userId && offerIds.Contains(o.Id))
            .ToListAsync(ct);

        if (offers.Count == 0) return 0;

        db.OffresEmploi.RemoveRange(offers);
        await db.SaveChangesAsync(ct);
        return offers.Count;
    }

    private async Task EnsureOfferOwnedAsync(Guid userId, Guid offerId, CancellationToken ct)
    {
        var exists = await db.OffresEmploi.AnyAsync(o => o.Id == offerId && o.UtilisateurId == userId, ct);
        if (!exists) throw new KeyNotFoundException($"Offer {offerId} not found.");
    }

    private static string NormalizeDraftJson(JsonElement draft)
    {
        var payload = draft.ValueKind == JsonValueKind.Object && draft.TryGetProperty("data", out var data)
            ? data
            : draft;
        var cvData = JsonSerializer.Deserialize<CvData>(payload.GetRawText(), CvJsonOptions) ?? new CvData();
        var sanitized = CvService.SanitizeCvData(cvData);
        return JsonSerializer.Serialize(sanitized, CvJsonOptions);
    }

    private static OfferAnalysisDto MapToDto(Guid offerId, JsonElement root, string? rawText = null)
    {
        var dto = new OfferAnalysisDto { OfferId = offerId, TexteBrut = rawText };

        if (root.TryGetProperty("analyzed_offer", out var ao) && ao.ValueKind == JsonValueKind.Object)
        {
            dto.Titre = ao.GetStringOrDefault("titre") ?? "";
            dto.Entreprise = ao.GetStringOrDefault("entreprise");
            dto.TypeContrat = ao.GetStringOrDefault("type_contrat");
            dto.Localisation = ao.GetStringOrDefault("localisation");
            dto.DescriptionPoste = ao.GetStringOrDefault("description_poste");
            dto.AnneesExperience = ao.GetStringAsIntOrDefault("annees_experience");
            dto.NiveauEtudes = ao.GetStringOrDefault("niveau_etudes");
            dto.ModeTravail = ao.GetStringOrDefault("mode_travail") ?? ao.GetStringOrDefault("modeTravail");
            dto.CompetencesRequises = ao.GetStringList("competences_requises");
            dto.CompetencesSouhaitees = ao.GetStringList("competences_souhaitees");
            dto.KeywordsAts = ao.GetStringList("keywords_ats");
        }

        if (root.TryGetProperty("cv_data", out var cvData) && cvData.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            dto.CvGeneratedContent = JsonSerializer.Deserialize<object>(cvData.GetRawText());
        }
        else if (root.TryGetProperty("cvData", out var cvDataCamel) && cvDataCamel.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            dto.CvGeneratedContent = JsonSerializer.Deserialize<object>(cvDataCamel.GetRawText());
        }

        if (root.TryGetProperty("profile_data", out var profileData) && profileData.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            dto.ProfileData = JsonSerializer.Deserialize<object>(profileData.GetRawText());
        }
        else if (root.TryGetProperty("profileData", out var profileDataCamel) && profileDataCamel.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            dto.ProfileData = JsonSerializer.Deserialize<object>(profileDataCamel.GetRawText());
        }

        if (root.TryGetProperty("skill_gap_analysis", out var sga) && sga.ValueKind == JsonValueKind.Object)
        {
            dto.SkillGapAnalysis = JsonSerializer.Deserialize<object>(sga.GetRawText());
            dto.MatchResult = dto.MatchResult ?? JsonSerializer.Deserialize<object>(sga.GetRawText());
            ApplySkillGapToDto(dto, sga);
        }

        if (root.TryGetProperty("match_result", out var mr) && mr.ValueKind == JsonValueKind.Object)
        {
            dto.MatchResult = JsonSerializer.Deserialize<object>(mr.GetRawText());
            dto.SkillGapAnalysis ??= JsonSerializer.Deserialize<object>(mr.GetRawText());
            ApplySkillGapToDto(dto, mr);
        }

        if (root.TryGetProperty("skill_gap", out var sg) && sg.ValueKind == JsonValueKind.Object)
        {
            dto.ScoreMatching = sg.GetIntOrDefault("score_matching") ?? sg.GetIntOrDefault("match_score") ?? dto.ScoreMatching;
            dto.ScoreAts = sg.GetIntOrDefault("score_ats") ?? sg.GetIntOrDefault("ats_score") ?? dto.ScoreAts;
            
            var sgKeywordsPresents = sg.GetStringList("keywords_presents");
            if (sgKeywordsPresents.Count > 0) dto.KeywordsPresents = sgKeywordsPresents;
            
            var sgKeywordsManquants = sg.GetStringList("keywords_manquants");
            if (sgKeywordsManquants.Count > 0) dto.KeywordsManquants = sgKeywordsManquants;
            
            var sgRecommandations = sg.GetStringList("recommandations");
            if (sgRecommandations.Count > 0) dto.Recommandations = sgRecommandations;
            
            var sgCompetencesMatching = sg.GetStringList("competences_matching");
            if (sgCompetencesMatching.Count > 0)
            {
                dto.CompetencesMatching = sgCompetencesMatching;
                if (dto.KeywordsPresents.Count == 0) dto.KeywordsPresents = sgCompetencesMatching;
            }
            
            var sgCompetencesManquantes = sg.GetStringList("competences_manquantes");
            if (sgCompetencesManquantes.Count > 0)
            {
                dto.CompetencesManquantes = sgCompetencesManquantes;
                if (dto.KeywordsManquants.Count == 0) dto.KeywordsManquants = sgCompetencesManquantes;
            }
            
            // Fallback: use matched_skills / missing_skills if competence_* are empty
            var sgMatched = sg.GetStringList("matched_skills");
            if (sgMatched.Count > 0 && dto.CompetencesMatching.Count == 0)
            {
                dto.CompetencesMatching = sgMatched;
                if (dto.KeywordsPresents.Count == 0) dto.KeywordsPresents = sgMatched;
            }
            
            var sgMissing = sg.GetStringList("missing_skills");
            if (sgMissing.Count > 0 && dto.CompetencesManquantes.Count == 0)
            {
                dto.CompetencesManquantes = sgMissing;
                if (dto.KeywordsManquants.Count == 0) dto.KeywordsManquants = sgMissing;
            }
        }

                // Deterministic fallback if skill_gap is empty/weak.
        if (dto.ScoreMatching == 0 || dto.ScoreAts == 0 || dto.CompetencesMatching.Count == 0)
        {
            var targets = dto.CompetencesRequises
                .Concat(dto.CompetencesSouhaitees)
                .Concat(dto.KeywordsAts)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (targets.Count > 0 && root.TryGetProperty("profile_data", out var pd) && pd.ValueKind == JsonValueKind.Object)
            {
                var evidenceTokens = ExtractProfileTokens(pd);
                var matched = new List<string>();
                var missing = new List<string>();
                var partial = new List<string>();

                foreach (var target in targets)
                {
                    var normalizedTarget = NormalizeSkill(target);
                    if (string.IsNullOrWhiteSpace(normalizedTarget)) continue;

                    var best = 0.0;
                    foreach (var token in evidenceTokens)
                    {
                        var simScore = ComputeSkillSimilarity(normalizedTarget, token);
                        if (simScore > best) best = simScore;
                    }

                    if (best >= 0.82) matched.Add(target);
                    else if (best >= 0.58) partial.Add(target);
                    else missing.Add(target);
                }

                var weightedMatched = matched.Count + (0.5 * partial.Count);
                var score = (int)Math.Round((weightedMatched / Math.Max(1, targets.Count)) * 100);
                score = Math.Min(98, Math.Max(0, score));

                if (dto.ScoreMatching == 0 || dto.CompetencesMatching.Count == 0)
                {
                    dto.ScoreMatching = score;
                    dto.CompetencesMatching = matched;
                    dto.CompetencesManquantes = missing.Concat(partial).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }

                if (dto.KeywordsPresents.Count == 0) dto.KeywordsPresents = matched;
                if (dto.KeywordsManquants.Count == 0) dto.KeywordsManquants = missing.Concat(partial).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (dto.ScoreAts == 0)
                {
                    var atsTargets = dto.KeywordsAts.Count > 0 ? dto.KeywordsAts : targets;
                    var atsPresent = atsTargets.Count(k => matched.Any(m => NormalizeSkill(m) == NormalizeSkill(k)));
                    var atsPartial = atsTargets.Count(k => partial.Any(p => NormalizeSkill(p) == NormalizeSkill(k)));
                    var atsWeighted = atsPresent + (0.5 * atsPartial);
                    dto.ScoreAts = Math.Min(98, Math.Max(0, (int)Math.Round((atsWeighted / Math.Max(1, atsTargets.Count)) * 100)));
                }

                if (dto.Recommandations.Count == 0)
                {
                    dto.Recommandations = missing
                        .Take(5)
                        .Select(m => $"Ajouter une preuve de '{m}' dans vos projets/experiences (impact, stack, resultat).")
                        .ToList();
                }
            }
        }

        // ATS fallback consistency: if ATS keywords lists are empty but matches exist,
        // infer ATS coverage from matched skills and ATS keywords.
        if (dto.KeywordsPresents.Count == 0 && dto.KeywordsAts.Count > 0 && dto.CompetencesMatching.Count > 0)
        {
            var matchedNorm = dto.CompetencesMatching.Select(NormalizeSkill).ToHashSet(StringComparer.OrdinalIgnoreCase);
            dto.KeywordsPresents = dto.KeywordsAts.Where(k => matchedNorm.Contains(NormalizeSkill(k))).ToList();
            dto.KeywordsManquants = dto.KeywordsAts.Where(k => !matchedNorm.Contains(NormalizeSkill(k))).ToList();

            if (dto.ScoreAts <= 5)
            {
                dto.ScoreAts = (int)Math.Round((double)dto.KeywordsPresents.Count / Math.Max(1, dto.KeywordsAts.Count) * 100);
            }
        }
        if (root.TryGetProperty("company_intelligence", out var ci) && ci.ValueKind == JsonValueKind.Object)
        {
            var intelligence = ci.ValueKind == JsonValueKind.Object && ci.TryGetProperty("intelligence", out var i) ? i : ci;
            
            var culture = intelligence.GetPropertyOrNull("culture");
            if (culture.HasValue)
            {
                dto.CompanyCultureScore = culture.Value.GetDoubleOrDefault("glassdoor_rating") ?? culture.Value.GetDoubleOrDefault("culture_score") ?? 0;
            }

            var salaries = intelligence.GetPropertyOrNull("salaries");
            if (salaries.HasValue && salaries.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in salaries.Value.EnumerateArray())
                {
                    dto.CompanySalaryMin = s.GetIntOrDefault("min_salary") ?? dto.CompanySalaryMin;
                    dto.CompanySalaryMax = s.GetIntOrDefault("max_salary") ?? dto.CompanySalaryMax;
                }
            }

            var actualites = intelligence.GetPropertyOrNull("actualites");
            if (actualites.HasValue && actualites.Value.ValueKind == JsonValueKind.Array)
            {
                dto.CompanyNews = [.. actualites.Value.EnumerateArray()
                    .Where(a => a.ValueKind == JsonValueKind.String)
                    .Select(a => new CompanyNewsItem { Title = a.GetString() ?? "", Date = "" })];
            }
        }

        if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            dto.Erreurs = [.. errors.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)];
        }

        return dto;
    }

    private static void ApplySkillGapToDto(OfferAnalysisDto dto, JsonElement skillGap)
    {
        dto.ScoreMatching = skillGap.GetIntOrDefault("score_matching") ?? skillGap.GetIntOrDefault("match_score") ?? dto.ScoreMatching;
        dto.ScoreAts = skillGap.GetIntOrDefault("score_ats") ?? skillGap.GetIntOrDefault("ats_score") ?? dto.ScoreAts;

        var keywordsPresents = skillGap.GetStringList("keywords_presents");
        if (keywordsPresents.Count > 0) dto.KeywordsPresents = keywordsPresents;

        var keywordsManquants = skillGap.GetStringList("keywords_manquants");
        if (keywordsManquants.Count > 0) dto.KeywordsManquants = keywordsManquants;

        var recommandations = skillGap.GetStringList("recommandations");
        if (recommandations.Count > 0) dto.Recommandations = recommandations;

        var competencesMatching = skillGap.GetStringList("competences_matching");
        if (competencesMatching.Count > 0)
        {
            dto.CompetencesMatching = competencesMatching;
            if (dto.KeywordsPresents.Count == 0) dto.KeywordsPresents = competencesMatching;
        }

        var competencesManquantes = skillGap.GetStringList("competences_manquantes");
        if (competencesManquantes.Count > 0)
        {
            dto.CompetencesManquantes = competencesManquantes;
            if (dto.KeywordsManquants.Count == 0) dto.KeywordsManquants = competencesManquantes;
        }

        var matchedSkills = skillGap.GetStringList("matched_skills");
        if (matchedSkills.Count > 0 && dto.CompetencesMatching.Count == 0)
        {
            dto.CompetencesMatching = matchedSkills;
            if (dto.KeywordsPresents.Count == 0) dto.KeywordsPresents = matchedSkills;
        }

        var missingSkills = skillGap.GetStringList("missing_skills");
        if (missingSkills.Count > 0 && dto.CompetencesManquantes.Count == 0)
        {
            dto.CompetencesManquantes = missingSkills;
            if (dto.KeywordsManquants.Count == 0) dto.KeywordsManquants = missingSkills;
        }
    }


    private static List<string> ExtractProfileTokens(JsonElement profileData)
    {
        var bag = new List<string>();

        var comps = profileData.GetPropertyOrNull("competences");
        if (comps.HasValue && comps.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in comps.Value.EnumerateArray())
            {
                var nom = c.GetStringOrDefault("nom") ?? c.GetStringOrDefault("name");
                if (!string.IsNullOrWhiteSpace(nom)) bag.Add(nom!);
            }
        }

        var rawSkills = profileData.GetPropertyOrNull("skills");
        if (rawSkills.HasValue && rawSkills.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in rawSkills.Value.EnumerateArray())
            {
                if (s.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(s.GetString())) bag.Add(s.GetString()!);
                else if (s.ValueKind == JsonValueKind.Object)
                {
                    var n = s.GetStringOrDefault("nom") ?? s.GetStringOrDefault("name");
                    if (!string.IsNullOrWhiteSpace(n)) bag.Add(n!);
                }
            }
        }

        ExtractTextFields(profileData.GetPropertyOrNull("experiences"), new[] { "poste", "position", "titre", "description", "technologies", "missions" }, bag);
        ExtractTextFields(profileData.GetPropertyOrNull("projets"), new[] { "nom", "title", "description", "technologies", "stack", "outils" }, bag);
        ExtractTextFields(profileData.GetPropertyOrNull("projects"), new[] { "nom", "title", "description", "technologies", "stack", "outils" }, bag);

        var resume = profileData.GetStringOrDefault("resume_professionnel") ?? profileData.GetStringOrDefault("resume") ?? profileData.GetStringOrDefault("summary");
        if (!string.IsNullOrWhiteSpace(resume)) bag.Add(resume!);

        return bag.Select(NormalizeSkill).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ExtractTextFields(JsonElement? arr, string[] fields, List<string> bag)
    {
        if (!arr.HasValue || arr.Value.ValueKind != JsonValueKind.Array) return;
        foreach (var item in arr.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            foreach (var field in fields)
            {
                var value = item.GetStringOrDefault(field);
                if (!string.IsNullOrWhiteSpace(value)) bag.Add(value!);
            }
        }
    }

    private static readonly Dictionary<string, string[]> SkillSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["javascript"] = new[] { "js", "ecmascript", "node", "nodejs", "node.js" },
        ["typescript"] = new[] { "ts" },
        ["react"] = new[] { "reactjs", "react.js", "nextjs", "next.js" },
        ["angular"] = new[] { "angularjs" },
        ["vue"] = new[] { "vuejs", "vue.js", "nuxt", "nuxtjs" },
        ["python"] = new[] { "fastapi", "django", "flask" },
        ["dotnet"] = new[] { ".net", "aspnet", "asp.net", "csharp", "c#" },
        ["java"] = new[] { "spring", "springboot", "spring boot" },
        ["postgresql"] = new[] { "postgres", "psql" },
        ["mongodb"] = new[] { "mongo" },
        ["docker"] = new[] { "container", "containers", "kubernetes", "k8s" },
        ["ci/cd"] = new[] { "github actions", "gitlab ci", "jenkins", "pipeline" },
        ["ai"] = new[] { "ml", "machine learning", "llm", "nlp", "chatbot", "rag" }
    };

    private static string NormalizeSkill(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var lower = value.ToLowerInvariant();
        lower = Regex.Replace(lower, @"[^\w\s\+#\.\/-]", " ");
        lower = Regex.Replace(lower, @"\s+", " ").Trim();
        return lower;
    }

    private static bool AreSynonyms(string left, string right)
    {
        if (left == right) return true;
        foreach (var kv in SkillSynonyms)
        {
            var family = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { kv.Key };
            foreach (var s in kv.Value) family.Add(NormalizeSkill(s));
            if (family.Contains(left) && family.Contains(right)) return true;
        }
        return false;
    }

    private static double ComputeSkillSimilarity(string target, string evidence)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(evidence)) return 0;
        if (target == evidence) return 1.0;
        if (AreSynonyms(target, evidence)) return 0.95;
        if (evidence.Contains(target) || target.Contains(evidence)) return 0.8;

        var targetWords = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var evidenceWords = evidence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = targetWords.Intersect(evidenceWords, StringComparer.OrdinalIgnoreCase).Count();
        if (overlap == 0) return 0;

        var jaccard = (double)overlap / Math.Max(1, targetWords.Union(evidenceWords, StringComparer.OrdinalIgnoreCase).Count());
        return Math.Min(0.78, 0.45 + jaccard * 0.5);
    }

    public async Task<OffreEmploi?> GetOfferWithAnalysisAsync(Guid offerId, CancellationToken ct = default)
    {
        return await repository.GetByIdWithAnalysisAsync(offerId, ct);
    }
}

public static class JsonElementExtensions
{
    public static string? GetStringOrDefault(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public static int? GetIntOrDefault(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Number)
            return null;
            
        // Use TryGetInt32 first, fallback to GetDouble and cast if it's a float
        if (v.TryGetInt32(out var i)) return i;
        return (int)v.GetDouble();
    }

    public static int? GetStringAsIntOrDefault(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
        if (v.ValueKind == JsonValueKind.String)
        {
            var str = v.GetString();
            if (string.IsNullOrEmpty(str)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(str, @"\d+");
            if (match.Success && int.TryParse(match.Value, out var parsed))
                return parsed;
        }
        return null;
    }

    public static List<string> GetStringList(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? [.. arr.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)]
            : [];

    public static double? GetDoubleOrDefault(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : null;

    public static JsonElement? GetPropertyOrNull(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? v : null;
}







