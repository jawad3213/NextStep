using NextStep.Shared.ErrorHandling;

namespace NextStep.Modules.Profile.Application.Validation;

/// <summary>Rules for an uploaded profile photo (throws <see cref="BadRequestException"/>).</summary>
public static class ProfilePhotoValidator
{
    public const long MaxBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/webp",
    };

    public static void Validate(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            throw new BadRequestException("Aucune image fournie.");

        if (file.Length > MaxBytes)
            throw new BadRequestException("L'image ne doit pas depasser 2MB.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new BadRequestException("Format d'image non supporte. Utilisez PNG, JPG ou WEBP.");
    }
}
