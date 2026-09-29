using System.Text.Json.Serialization;
using NextStep.Modules.Profile.Domain;

namespace NextStep.Modules.Profile.Application.Dtos;

/// <summary>A skill-keyword suggestion: <c>{ mot, categorie }</c>.</summary>
public sealed record KeywordDto(string Mot, string Categorie);

public sealed record PhotoUploadResponse(string PhotoUrl, string ObjectKey, string Message);

/// <summary>Displayable profile photo URL (signed when stored in object storage).</summary>
public sealed record PhotoUrlResponse(
    string? PhotoUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ObjectKey = null);

public sealed record PhotoFile(byte[] Content, string ContentType);

/// <summary>AI resume generation; degrades to an empty resume plus <see cref="Errors"/>.</summary>
public sealed record GenerateResumeResponse(
    string Resume,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? Errors = null);

/// <summary>The local user, as returned by GET /api/identity/profile.</summary>
public sealed record UserProfileDto(
    Guid Id,
    string KeycloakId,
    string? Nom,
    string? Prenom,
    string Email,
    string? LienLinkedin,
    string? LienGithub,
    string? LienPortfolio,
    string? TitrePoste,
    string? PhotoUrl,
    string? Ville,
    string? Pays,
    string? Telephone,
    string? ResumeProfessionnel,
    string? Coordonnees,
    string? TitresSections,
    string? Objectif,
    string? Niveau,
    string? Secteur,
    bool OnboardingCompleted,
    bool ProfileCompleted,
    int OnboardingStep,
    string? OnboardingData,
    int ProfileScore,
    DateTime DateInscription)
{
    public static UserProfileDto From(UserEntity u) => new(
        u.Id, u.KeycloakId, u.Nom, u.Prenom, u.Email, u.LienLinkedin, u.LienGithub, u.LienPortfolio,
        u.TitrePoste, u.PhotoUrl, u.Ville, u.Pays, u.Telephone, u.ResumeProfessionnel, u.Coordonnees,
        u.TitresSections, u.Objectif, u.Niveau, u.Secteur, u.OnboardingCompleted, u.ProfileCompleted,
        u.OnboardingStep, u.OnboardingData, u.ProfileScore, u.DateInscription);
}

public sealed record UserProfileResponse(string Message, UserProfileDto Data);

public sealed record OnboardingStatusResponse(
    bool OnboardingCompleted,
    bool ProfileCompleted,
    int ProfileScore,
    int CompletionPercent);

public sealed record SoftOnboardingData(bool OnboardingCompleted);

public sealed record SoftOnboardingResponse(string Message, SoftOnboardingData Data);

/// <summary>Standard error contract (<c>error</c>) plus the completion figures the stepper shows.</summary>
public sealed record CompleteProfileErrorResponse(string Error, int CompletionPercent, int RequiredPercent);
