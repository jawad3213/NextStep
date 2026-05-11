using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace NextStep.Shared.Storage;

/// <summary>
/// Configuration for the MinIO / S3-compatible object store.
/// </summary>
public class MinioOptions
{
    public string Endpoint { get; set; } = "http://minio:9000";
    public string AccessKey { get; set; } = "admin";
    public string SecretKey { get; set; } = "admin123";
    public string BucketName { get; set; } = "nextstep-cvs";

    /// <summary>
    /// Public base URL used to build download links.
    /// Inside Docker this is the minio service; from the browser it's localhost:9000.
    /// </summary>
    public string PublicUrl { get; set; } = "http://localhost:9000";
}

public interface IStorageService
{
    /// <summary>
    /// Uploads a file and returns the public URL.
    /// </summary>
    Task<string> UploadFileAsync(string objectKey, byte[] data, string contentType);

    /// <summary>
    /// Generates a pre-signed download URL valid for the given duration.
    /// </summary>
    Task<string> GetPresignedUrlAsync(string objectKey, TimeSpan? expiry = null);

    /// <summary>
    /// Ensures that the configured bucket exists in MinIO.
    /// </summary>
    Task EnsureBucketExistsAsync();
}

public class MinioStorageService : IStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly MinioOptions _options;

    public MinioStorageService(IAmazonS3 s3, IOptions<MinioOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<string> UploadFileAsync(string objectKey, byte[] data, string contentType)
    {
        // Ensure the bucket exists
        await EnsureBucketExistsAsync();

        using var stream = new MemoryStream(data);

        var request = new PutObjectRequest
        {
            BucketName  = _options.BucketName,
            Key         = objectKey,
            InputStream = stream,
            ContentType = contentType,
        };

        await _s3.PutObjectAsync(request);

        // Return a public-style URL
        return $"{_options.PublicUrl.TrimEnd('/')}/{_options.BucketName}/{objectKey}";
    }

    public async Task<string> GetPresignedUrlAsync(string objectKey, TimeSpan? expiry = null)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key        = objectKey,
            Expires    = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromHours(1)),
            Verb       = HttpVerb.GET,
        };

        return await Task.FromResult(_s3.GetPreSignedURL(request));
    }

    public async Task EnsureBucketExistsAsync()
    {
        try
        {
            await _s3.PutBucketAsync(new PutBucketRequest
            {
                BucketName = _options.BucketName,
            });
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "BucketAlreadyOwnedByYou"
                                        || ex.ErrorCode == "BucketAlreadyExists")
        {
            // Bucket already exists — that's fine.
        }
    }
}
