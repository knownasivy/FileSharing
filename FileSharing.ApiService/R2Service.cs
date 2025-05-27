using Amazon.Runtime;
using Amazon.S3;

namespace FileSharing.ApiService;

// TODO: Move to new way of getting options from config?
public class R2Config
{
    public required string AccessKey { get; init; } = string.Empty;
    public required string SecretKey { get; init; } = string.Empty;
    public required string AccountId { get; init; } = string.Empty;
}

public static class R2Service
{
    public static IAmazonS3 GetR2Config(R2Config r2Config)
    {
        var credentials = new BasicAWSCredentials(
            r2Config.AccessKey, 
            r2Config.SecretKey);
        
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{r2Config.AccountId}.r2.cloudflarestorage.com",
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        return new AmazonS3Client(credentials, config);
    }
}