using NextStep.Modules.Messaging.Infrastructure.Gmail;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextStep.Modules.Applications.Contracts;
using NextStep.Modules.CvDocuments.Contracts;
using NextStep.Modules.Messaging.Infrastructure.Persistence;
using NextStep.Modules.Messaging.Application.Dtos;
using NextStep.Modules.Messaging.Application.Mappings;
using NextStep.Modules.Messaging.Domain;
using NextStep.Modules.Messaging.Infrastructure.Repositories;
using NextStep.Modules.Profile.Contracts;
using NextStep.Shared.Config;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Http;

namespace NextStep.Modules.Messaging.Application.Services;

/// <summary>
/// Candidate + job offer context sent to the email agent, read through the Profile and
/// Applications contracts.
/// </summary>
public class EmailContextBuilder(IApplicationsApi applications, IProfileApi profile, ILogger<EmailContextBuilder> logger)
{
    private readonly IApplicationsApi _applications = applications;
    private readonly IProfileApi _profile = profile;
    private readonly ILogger<EmailContextBuilder> _logger = logger;

    public async Task<EmailContext> BuildAsync(
        Guid userId,
        Guid offreId,
        CancellationToken cancellationToken)
    {
        var profile = await _profile.GetCandidateProfileAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} not found.");

        var offre = await _applications.GetOfferContentAsync(offreId, cancellationToken);

        string? jobTitle = null;
        string? companyName = null;
        string? location = null;
        var requiredSkills  = new List<string>();
        var preferredSkills = new List<string>();
        var missions        = new List<string>();
        var requirements    = new List<string>();
        string? skillGapJson = null;
        string? companyIntelligenceJson = null;

        if (offre?.AnalysisJson is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(offre.AnalysisJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("job_title", out var jt))   jobTitle    = jt.GetString();
                if (root.TryGetProperty("company_name", out var cn)) companyName = cn.GetString();
                if (root.TryGetProperty("location", out var loc))    location    = loc.GetString();

                requiredSkills  = ExtractStringList(root, "required_skills");
                preferredSkills = ExtractStringList(root, "preferred_skills");
                missions        = ExtractStringList(root, "missions");
                requirements    = ExtractStringList(root, "requirements");

                // Extract enrichment if the full pipeline JSON was saved
                if (root.TryGetProperty("match_result", out var mr)) skillGapJson = mr.GetRawText();
                if (root.TryGetProperty("company_intelligence", out var ci)) companyIntelligenceJson = ci.GetRawText();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not parse AnalyseJson for offer {OfferId} — will use raw text only.", offreId);
            }
        }

        return new EmailContext(
            FullName:        profile.User.FullName,
            Email:           profile.User.Email,
            Phone:           profile.Phone,
            CurrentTitle:    profile.CurrentTitle,
            Skills:          profile.Skills.ToList(),
            Experiences:     profile.Experiences.Select(e =>
                $"{e.Title} at {e.Company}" +
                $" ({e.StartDate?.Year}\u2013{(e.EndDate.HasValue ? e.EndDate.Value.Year.ToString() : "present")})").ToList(),
            Education:       profile.Education.Select(f =>
                $"{f.Degree} \u2013 {f.School} ({f.Year})").ToList(),
            Projects:        profile.Projects.ToList(),
            Certifications:  profile.Certifications.ToList(),
            JobTitle:        jobTitle ?? "Position not specified",
            CompanyName:     companyName,
            Location:        location,
            RequiredSkills:  requiredSkills,
            PreferredSkills: preferredSkills,
            Missions:        missions,
            Requirements:    requirements,
            RawText:         offre?.RawText,
            SkillGapJson:        skillGapJson,
            CompanyIntelligenceJson: companyIntelligenceJson);
    }

    public static object CandidatePayload(EmailContext ctx) => new
    {
        full_name     = ctx.FullName,
        email         = ctx.Email,
        phone         = ctx.Phone,
        current_title = ctx.CurrentTitle,
        skills        = ctx.Skills,
        experiences   = ctx.Experiences,
        education     = ctx.Education,
        projects      = ctx.Projects,
        certifications = ctx.Certifications,
    };

    public static object JobOfferPayload(EmailContext ctx) => new
    {
        job_title        = ctx.JobTitle,
        company_name     = ctx.CompanyName,
        location         = ctx.Location,
        required_skills  = ctx.RequiredSkills,
        preferred_skills = ctx.PreferredSkills,
        missions         = ctx.Missions,
        requirements     = ctx.Requirements,
        raw_text         = ctx.RawText,
        analysis_json    = (object?)null,
    };

    private static List<string> ExtractStringList(JsonElement root, string property)
    {
        if (root.TryGetProperty(property, out var arr) &&
            arr.ValueKind == JsonValueKind.Array)
        {
            return arr.EnumerateArray()
                      .Select(x => x.GetString() ?? "")
                      .Where(s => s.Length > 0)
                      .ToList();
        }
        return [];
    }

    public static object? DeserializeJsonObjectOrNull(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<object>(rawJson);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Candidate and job-offer data an email is written from.</summary>
public sealed record EmailContext(
    string FullName,
    string? Email,
    string? Phone,
    string? CurrentTitle,
    List<string> Skills,
    List<string> Experiences,
    List<string> Education,
    List<string> Projects,
    List<string> Certifications,
    string JobTitle,
    string? CompanyName,
    string? Location,
    List<string> RequiredSkills,
    List<string> PreferredSkills,
    List<string> Missions,
    List<string> Requirements,
    string? RawText,
    string? SkillGapJson = null,
    string? CompanyIntelligenceJson = null);
