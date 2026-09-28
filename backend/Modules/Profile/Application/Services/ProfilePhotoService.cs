using NextStep.Modules.Profile.Infrastructure.Persistence;
using NextStep.Modules.Profile.Application.Dtos;
using NextStep.Modules.Profile.Application.Validation;
using NextStep.Shared.ErrorHandling;
using NextStep.Shared.Storage;

namespace NextStep.Modules.Profile.Application.Services;

public interface IProfilePhotoService
{
    Task<PhotoUploadResponse> UploadAsync(Guid userId, IFormFile? file);
    Task<PhotoFile> GetPhotoAsync(Guid userId);
    Task<PhotoUrlResponse> GetSignedUrlAsync(Guid userId);
}

/// <summary>Profile photo stored in object storage; the user row keeps its URL.</summary>
public class ProfilePhotoService(ProfileDbContext db, IStorageService storage) : IProfilePhotoService
{
    public async Task<PhotoUploadResponse> UploadAsync(Guid userId, IFormFile? file)
    {
        ProfilePhotoValidator.Validate(file);

        var user = await db.Utilisateurs.FindAsync(userId)
            ?? throw new NotFoundException("Utilisateur introuvable.");

        var extension = Path.GetExtension(file!.FileName);
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

        var photoUrl = await storage.UploadFileAsync(objectKey, memory.ToArray(), file.ContentType);
        user.PhotoUrl = photoUrl;
        await db.SaveChangesAsync();

        return new PhotoUploadResponse(photoUrl, objectKey, "Photo de profil televersee avec succes.");
    }

    public async Task<PhotoFile> GetPhotoAsync(Guid userId)
    {
        var user = await db.Utilisateurs.FindAsync(userId)
            ?? throw new NotFoundException("Utilisateur non trouve.");

        var objectKey = ExtractObjectKey(user.PhotoUrl);
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new NotFoundException("Photo de profil introuvable.");

        var bytes = await storage.DownloadFileAsync(objectKey);
        return new PhotoFile(bytes, ResolveImageContentType(objectKey));
    }

    public async Task<PhotoUrlResponse> GetSignedUrlAsync(Guid userId)
    {
        var user = await db.Utilisateurs.FindAsync(userId)
            ?? throw new NotFoundException("Utilisateur non trouve.");

        var rawUrl = (user.PhotoUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawUrl))
            return new PhotoUrlResponse(null);

        // Not an object-storage URL (/{bucket}/{objectKey}): returned as-is.
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var parsed))
            return new PhotoUrlResponse(rawUrl);

        var path = parsed.AbsolutePath.Trim('/');
        var slashIdx = path.IndexOf('/');
        if (slashIdx <= 0 || slashIdx >= path.Length - 1)
            return new PhotoUrlResponse(rawUrl);

        var objectKey = path[(slashIdx + 1)..];
        if (string.IsNullOrWhiteSpace(objectKey))
            return new PhotoUrlResponse(rawUrl);

        try
        {
            var signedUrl = await storage.GetPresignedUrlAsync(objectKey, TimeSpan.FromHours(6));
            return new PhotoUrlResponse(signedUrl, objectKey);
        }
        catch
        {
            // Fallback to the stored URL if signing fails
            return new PhotoUrlResponse(rawUrl);
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

    private static string ResolveImageContentType(string objectKey) =>
        Path.GetExtension(objectKey).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
}
