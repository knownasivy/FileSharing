using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Caching.Hybrid;

namespace FileSharing.Api.Services;

public interface ICloudService
{
    Task<bool> UploadAsync(string key, string filePath, string contentType);
    Task<bool> UploadAsync(string key, Stream fileStream, string contentType);
    Task<bool> GetExistsAsync(string key);
    Task<string?> GetPreviewFileUrl(string key);
}

// TODO: Add other methods like uploading etc
public class CloudService : ICloudService
{
    private const string Bucket = "files";
    
    private readonly ILogger<CloudService> _logger;
    private readonly HybridCache _cache;
    private readonly AmazonS3Client _s3;

    public CloudService(
        HybridCache cache,
        ILogger<CloudService> logger, 
        IConfiguration configuration)
    {
        _cache = cache;
        _logger = logger;
        
        var credentials = new BasicAWSCredentials(
            configuration["R2:AccessKey"], 
            configuration["R2:SecretKey"]);

        var accountId = configuration["R2:AccountId"];
        
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        _s3 = new AmazonS3Client(credentials, config);
    }

    public async Task<bool> UploadAsync(string key, string filePath, string contentType)
    {
        var request = new PutObjectRequest
        {
            BucketName = Bucket,
            Key = key,
            FilePath = filePath,
            ContentType = contentType,
            DisablePayloadSigning = true
        };

        try
        {
            await _s3.PutObjectAsync(request);
            await _cache.RemoveAsync($"exists:{key}");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return false;
        }
        
        return true;
    }

    public async Task<bool> UploadAsync(string key, Stream fileStream, string contentType)
    {
        var request = new PutObjectRequest
        {
            BucketName = Bucket,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType,
            DisablePayloadSigning = true
        };

        try
        {
            await _s3.PutObjectAsync(request);
            await _cache.RemoveAsync($"exists:{key}");
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            return false;
        }
        
        return true;
    }

    public async Task<bool> GetExistsAsync(string key)
    {
        var result = await _cache.GetOrCreateAsync(
            key: $"exists:{key}",
            factory: async ct =>
            {
                try
                {
                    await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
                    {
                        BucketName = Bucket,
                        Key = key
                    }, ct);

                    return true;
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(55),
                LocalCacheExpiration = TimeSpan.FromMinutes(55)
            }
        );

        return result;
    }
    
    public async Task<string?> GetPreviewFileUrl(string key)
    {
        var result = await _cache.GetOrCreateAsync<string?>(
            key: $"preview:{key}",
            factory: async ct =>
            {
                _logger.LogInformation("Caching: \"preview:{Key}\"", key);
                return await PreviewFactory(key, ct);
            },
            options: new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(55),
                LocalCacheExpiration = TimeSpan.FromMinutes(55)
            }
        );

        return result;
    }
    
    private async Task<string?> PreviewFactory(string key, CancellationToken token)
    {
        //_logger.LogInformation("key: {key}", key);
        try
        {
            await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = Bucket,
                Key = key
            }, token);
     
            var request = new GetPreSignedUrlRequest
            {
                BucketName = Bucket,
                Key = key,
                Expires = DateTime.UtcNow.AddMinutes(60),
                Verb = HttpVerb.GET
            };
                    
            return await _s3.GetPreSignedURLAsync(request);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    } 
}