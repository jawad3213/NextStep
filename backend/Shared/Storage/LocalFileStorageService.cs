using Microsoft.Extensions.Options;

namespace NextStep.Shared.Storage;

/// <summary>
/// Configuration for the local filesystem storage used when
/// Storage:Mode = "Local" (no MinIO / S3 in local development).
/// </summary>
public class LocalStorageOptions
{
    /// <summary>
    /// Absolute path of the storage root directory (e.g. {ContentRoot}/storage).
    /// </summary>
    public string RootPath { get; set; } = "storage";

    /// <summary>
    /// Public base URL the backend is reachable at, used to build file URLs.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// URL path prefix the static-file middleware serves the storage root from.
    /// </summary>
    public string RequestPath { get; set; } = "/uploads";
}

/// <summary>
/// IStorageService implementation that persists files on the local disk and
/// serves them back through the backend static-file middleware (/uploads).
/// Used only when MinIO is not available (local development).
/// </summary>
public class LocalFileStorageService : IStorageService
{
    private readonly LocalStorageOptions _options;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IOptions<LocalStorageOptions> options, ILogger<LocalFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private string ResolvePath(string objectKey)
    {
        var safeKey = (objectKey ?? string.Empty).Replace('\\', '/').TrimStart('/');
        var root = Path.GetFullPath(_options.RootPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, safeKey));

        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid object key.", nameof(objectKey));
        }

        return fullPath;
    }

    private void EnsureRoot()
    {
        Directory.CreateDirectory(Path.GetFullPath(_options.RootPath));
    }

    public Task EnsureBucketExistsAsync()
    {
        EnsureRoot();
        return Task.CompletedTask;
    }

    public async Task<string> UploadFileAsync(string objectKey, byte[] data, string contentType)
    {
        var path = ResolvePath(objectKey);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(path, data);

        var url = $"{_options.PublicBaseUrl.TrimEnd('/')}{_options.RequestPath}/{objectKey.TrimStart('/')}";
        _logger.LogInformation("Stored file locally: {Path} -> {Url}", path, url);
        return url;
    }

    public Task<string> GetPresignedUrlAsync(string objectKey, TimeSpan? expiry = null)
    {
        // No signing needed in local mode; the URL is stable and public.
        var url = $"{_options.PublicBaseUrl.TrimEnd('/')}{_options.RequestPath}/{objectKey.TrimStart('/')}";
        return Task.FromResult(url);
    }

    public async Task<byte[]> DownloadFileAsync(string objectKey)
    {
        var path = ResolvePath(objectKey);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {objectKey}", path);
        }

        return await File.ReadAllBytesAsync(path);
    }
}