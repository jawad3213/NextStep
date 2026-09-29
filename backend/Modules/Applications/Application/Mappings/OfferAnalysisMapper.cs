using System.Text.Json;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Services;
using NextStep.Shared.Json;

namespace NextStep.Modules.Applications.Application.Mappings;

/// <summary>
/// Builds the offer analysis DTO from the stored pipeline JSON (analysed offer, skill gap,
/// match result, generated CV), whichever of the agents' formats it is in.
/// </summary>
public static class OfferAnalysisMapper
{
    public static OfferAnalysisDto ToAnalysisDto(Guid offerId, JsonElement root, string? rawText = null)
    {
        var dto = new OfferAnalysisDto { OfferId = offerId, TexteBrut = rawText };

        // 1. Try to get data from AI Agent format ("analyzed_offer" property)
        // OR fallback to the root level (Manual Submission format)
        var source = root.TryGetProperty("analyzed_offer", out var ao) ? ao : root;

        dto.Titre = source.GetStringOrDefault("titre") ?? "Position not defined";
        dto.Entreprise = source.GetStringOrDefault("entreprise") ?? "Company not specified";
        dto.TypeContrat = source.GetStringOrDefault("type_contrat");
        dto.Localisation = source.GetStringOrDefault("localisation");
        dto.DescriptionPoste = source.GetStringOrDefault("description_poste");
        dto.AnneesExperience = source.GetIntOrDefault("annees_experience");
        dto.NiveauEtudes = source.GetStringOrDefault("niveau_etudes");
        dto.CompetencesRequises = source.GetStringList("competences_requises");
        dto.CompetencesSouhaitees = source.GetStringList("competences_souhaitees");
        dto.KeywordsAts = source.GetStringList("keywords_ats");

        // 2. Try to get scoring from AI Agent format ("match_result" property)
        if (root.TryGetProperty("analyzed_offer", out var analyzedOffer) && analyzedOffer.ValueKind == JsonValueKind.Object)
        {
            dto.Titre = analyzedOffer.GetStringOrDefault("titre") ?? "";
            dto.Entreprise = analyzedOffer.GetStringOrDefault("entreprise");
            dto.TypeContrat = analyzedOffer.GetStringOrDefault("type_contrat");
            dto.Localisation = analyzedOffer.GetStringOrDefault("localisation");
            dto.DescriptionPoste = analyzedOffer.GetStringOrDefault("description_poste");
            dto.AnneesExperience = analyzedOffer.GetStringAsIntOrDefault("annees_experience");
            dto.NiveauEtudes = analyzedOffer.GetStringOrDefault("niveau_etudes");
            dto.ModeTravail = analyzedOffer.GetStringOrDefault("mode_travail") ?? analyzedOffer.GetStringOrDefault("modeTravail");
            dto.CompetencesRequises = analyzedOffer.GetStringList("competences_requises");
            dto.CompetencesSouhaitees = analyzedOffer.GetStringList("competences_souhaitees");
            dto.KeywordsAts = analyzedOffer.GetStringList("keywords_ats");
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
                var evidenceTokens = SkillMatcher.ExtractProfileTokens(pd);
                var matched = new List<string>();
                var missing = new List<string>();
                var partial = new List<string>();

                foreach (var target in targets)
                {
                    var normalizedTarget = SkillMatcher.NormalizeSkill(target);
                    if (string.IsNullOrWhiteSpace(normalizedTarget)) continue;

                    var best = 0.0;
                    foreach (var token in evidenceTokens)
                    {
                        var simScore = SkillMatcher.ComputeSkillSimilarity(normalizedTarget, token);
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
                    var atsPresent = atsTargets.Count(k => matched.Any(m => SkillMatcher.NormalizeSkill(m) == SkillMatcher.NormalizeSkill(k)));
                    var atsPartial = atsTargets.Count(k => partial.Any(p => SkillMatcher.NormalizeSkill(p) == SkillMatcher.NormalizeSkill(k)));
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
            var matchedNorm = dto.CompetencesMatching.Select(SkillMatcher.NormalizeSkill).ToHashSet(StringComparer.OrdinalIgnoreCase);
            dto.KeywordsPresents = dto.KeywordsAts.Where(k => matchedNorm.Contains(SkillMatcher.NormalizeSkill(k))).ToList();
            dto.KeywordsManquants = dto.KeywordsAts.Where(k => !matchedNorm.Contains(SkillMatcher.NormalizeSkill(k))).ToList();

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
}
