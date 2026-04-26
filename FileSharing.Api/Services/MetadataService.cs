using System.Collections.Immutable;
using System.Diagnostics;
using Dapper;
using FFMpegCore;
using FileSharing.Api.Database;
using FileSharing.Api.Models;
using FileSharing.Api.Shared;
using InterpolatedSql.Dapper;
using Microsoft.Extensions.Caching.Hybrid;
using ZLinq;

namespace FileSharing.Api.Services;

public interface IMetadataService
{
    // TODO: Better name?
    // Task<IMetadata?> CreateAsync(FileUpload file, string filePath);
    // Task<IMetadata?> GetByFileAsync(FileUpload file);

    Task<bool> ProcessAudioFileAsync(UploadFile file);
    Task<AudioMetadata?> GetAudioMetadataByIdAsync(Guid fileId);
    Task<bool> HandlePreviewFileAsync(UploadFile file);
}

public class MetadataService(
    ICloudService cloudService,
    HybridCache cache,
    IDbConnectionFactory connectionFactory,
    ILogger<MetadataService> logger
) : IMetadataService
{
    private const string CachePrefix = nameof(MetadataService);
    private static readonly TimeSpan MaxAudioDuration = TimeSpan.FromMinutes(45);
    private const long MaxPreviewFileSize = 3 * BytesSize.MiB;
    private static readonly ImmutableHashSet<string> AudioKeysToKeep =
    [
        "title",
        "artist",
        "album",
        "album_artist",
    ];

    public record PreviewFileResult
    {
        public bool Success { get; init; }
    }

    public async Task<bool> ProcessAudioFileAsync(UploadFile file)
    {
        if (file.Status != FileStatus.Uploaded)
            throw new InvalidOperationException("File not uploaded");

        if (file.Type != FileType.Audio || !file.CanExtractMetadata())
            return true;

        var metadata = await CreateAudioMetadataAsync(file);
        if (metadata is null)
            return false;

        await using var connection = await connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO AudioMetadata (FileHash, Title, Album, Artist, AttachedPic, BitRate)
            VALUES (@FileHash, @Title, @Album, @Artist, @AttachedPic, @BitRate);
            """,
            metadata
        );

        return true;
    }

    public async Task<bool> HandlePreviewFileAsync(UploadFile file)
    {
        var previewFileName = $"{file.Id:N}_prev.mp4";
        var localPreview = Path.Combine(file.GetLocation(), previewFileName);

        List<string> tempFiles = [localPreview];

        try
        {
            var uploadFile = await DetermineUploadFileAsync(file, localPreview, tempFiles);

            if (string.IsNullOrEmpty(uploadFile) || !File.Exists(uploadFile))
                return false;

            await UploadPreviewFileAsync(uploadFile, previewFileName.Replace(".mp4", ""), file.Id);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle preview file for {FileId}", file.Id);
            return false;
        }
        finally
        {
            var filesToClean = tempFiles
                .Concat([localPreview])
                .Where(f => !string.IsNullOrEmpty(f));

            await CleanupFilesAsync(filesToClean);
        }
    }

    private async Task<AudioMetadata?> CreateAudioMetadataAsync(UploadFile file)
    {
        if (file.Type != FileType.Audio)
            throw new ArgumentException("File must be of type Audio", nameof(file));

        var mediaInfo = await FFProbe.AnalyseAsync(file.GetThisFilePath(), new FFOptions());

        if (mediaInfo.Duration > MaxAudioDuration || mediaInfo.ErrorData.Count != 0)
            return null;

        var metadata = new AudioMetadata { FileHash = file.Hash };

        ExtractAttachedPicture(mediaInfo, metadata);

        if (!ExtractBitRate(mediaInfo, metadata))
            return null;

        ExtractTagMetadata(mediaInfo, metadata);

        if (metadata.AttachedPic)
            await HandleCoverArtAsync(file);

        return metadata;
    }

    private static void ExtractAttachedPicture(IMediaAnalysis mediaInfo, AudioMetadata metadata)
    {
        var videoStream = mediaInfo.PrimaryVideoStream;
        if (
            videoStream?.Disposition != null
            && videoStream.Disposition.TryGetValue("attached_pic", out var picValue)
        )
        {
            metadata.AttachedPic = picValue;
        }
    }

    private static bool ExtractBitRate(IMediaAnalysis mediaInfo, AudioMetadata metadata)
    {
        if (mediaInfo.PrimaryAudioStream?.BitRate is null)
            return false;

        metadata.BitRate = (int)Math.Round(mediaInfo.PrimaryAudioStream.BitRate / 1000.0);
        return true;
    }

    private static void ExtractTagMetadata(IMediaAnalysis mediaInfo, AudioMetadata metadata)
    {
        if (mediaInfo.Format.Tags is null)
            return;

        var filteredTags = mediaInfo
            .Format.Tags.AsValueEnumerable()
            .Where(kv => AudioKeysToKeep.Contains(kv.Key))
            .ToImmutableList();

        string? albumArtist = null;

        foreach (var (key, value) in filteredTags)
        {
            switch (key.ToLowerInvariant())
            {
                case "title":
                    metadata.Title = value;
                    break;
                case "artist":
                    metadata.Artist = value;
                    break;
                case "album":
                    metadata.Album = value;
                    break;
                case "album_artist":
                    albumArtist = value;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(metadata.Artist) && !string.IsNullOrWhiteSpace(albumArtist))
            metadata.Artist = albumArtist;
    }

    public async Task<AudioMetadata?> GetAudioMetadataByIdAsync(Guid fileId)
    {
        return await cache.GetOrCreateAsync(
            $"{CachePrefix}:{nameof(GetAudioMetadataByIdAsync)}:{fileId}",
            async token => await FromDb(token),
            options: new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(3),
                Expiration = TimeSpan.FromMinutes(3),
            },
            cancellationToken: CancellationToken.None
        );

        async Task<AudioMetadata?> FromDb(CancellationToken ct)
        {
            await using var connection = await connectionFactory.CreateConnectionAsync(ct);
            var query = connection.SqlBuilder(
                $"""
                SELECT audio.*
                FROM UploadFiles file
                    JOIN AudioMetadata audio ON audio.FileHash = file.Hash
                WHERE file.Id = {fileId}
                LIMIT 1
                """
            );

            return await query.QueryFirstOrDefaultAsync<AudioMetadata>(cancellationToken: ct);
        }
    }

    private async Task<string?> DetermineUploadFileAsync(
        UploadFile file,
        string localPreview,
        List<string> tempFiles
    )
    {
        var hasLocalPreview = file.Extension is "m4a" or "mp3" && File.Exists(localPreview);

        logger.LogInformation("Has local preview: {Value}", hasLocalPreview);

        if (hasLocalPreview)
        {
            var fileInfo = new FileInfo(localPreview);
            if (fileInfo.Length <= MaxPreviewFileSize)
                return localPreview;
        }

        var convertedFile = await ConvertAudioToPreviewFileAsync(file.GetThisFilePath());
        if (!string.IsNullOrEmpty(convertedFile))
            tempFiles.Add(convertedFile);

        return convertedFile;
    }

    private async Task UploadPreviewFileAsync(
        string uploadFile,
        string previewFileName,
        Guid fileId
    )
    {
        await using var fs = new FileStream(
            uploadFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            StorageConfig.MediumBufferSize,
            useAsync: true
        );

        await cloudService.UploadAsync(previewFileName, uploadFile, "video/mp4");
        await cache.RemoveAsync($"UploadFileService:GetDownloadFileByIdAsync:{fileId}");
        await cache.RemoveAsync($"preview:{previewFileName}");
    }

    private async Task HandleCoverArtAsync(UploadFile file)
    {
        var coverFileName = $"{file.Id:N}_cover";
        var coverFile = await ExtractAudioCoverArt(file);

        var tempFiles = new List<string>();
        if (!string.IsNullOrEmpty(coverFile))
            tempFiles.Add(coverFile);

        try
        {
            if (!string.IsNullOrEmpty(coverFile) && File.Exists(coverFile))
                await cloudService.UploadAsync(coverFileName, coverFile, "image/webp");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload cover art for file {FileId}", file.Id);
        }
        finally
        {
            await CleanupFilesAsync(tempFiles);
        }
    }

    private async Task CleanupFilesAsync(IEnumerable<string> filePaths)
    {
        await Task.Delay(5000);

        var cleanupTasks = filePaths
            .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path))
            .Select(DeleteFileWithLoggingAsync);

        await Task.WhenAll(cleanupTasks);
    }

    private async Task DeleteFileWithLoggingAsync(string filePath)
    {
        try
        {
            await Task.Run(() => File.Delete(filePath));
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Access denied when trying to delete file: {FilePath}", filePath);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "I/O error when trying to delete file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error when trying to delete file: {FilePath}",
                filePath
            );
        }
    }

    public static async Task<string?> ConvertAudioToPreviewFileFastAsync(UploadFile file)
    {
        var outputFile = Path.GetFullPath(
            Path.Combine(file.GetLocation(), $"{file.Id:N}_prev.mp4")
        );
        var coverVideoPath = Environment.GetEnvironmentVariable("COVER_VIDEO_PATH") ?? "cover.mp4";

        List<string> args =
        [
            "-y",
            "-stream_loop",
            "-1",
            "-i",
            coverVideoPath,
            "-i",
            file.GetThisFilePath(),
            "-c:v",
            "copy",
            "-c:a",
            "aac",
            "-b:a",
            "76k",
            "-aac_coder",
            "fast",
            "-shortest",
            "-hide_banner",
            "-loglevel",
            "error",
            outputFile,
        ];

        var exitCode = await ExecuteFFmpegCommandAsync(args);
        if (exitCode == 0)
            return outputFile;

        if (File.Exists(outputFile))
            File.Delete(outputFile);

        return null;
    }

    private static async Task<string?> ConvertAudioToPreviewFileAsync(string inputFilePath)
    {
        var outputFile = Path.GetFullPath(Path.Combine("Uploads", $"{Guid.NewGuid():N}.mp4"));

        var coverVideoPath = Environment.GetEnvironmentVariable("COVER_VIDEO_PATH") ?? "cover.mp4";

        List<string> args =
        [
            "-y",
            "-stream_loop",
            "-1",
            "-i",
            coverVideoPath,
            "-i",
            inputFilePath,
            "-c:v",
            "copy",
            "-c:a",
            "libfdk_aac",
            "-b:a",
            "96k",
            "-shortest",
            "-movflags",
            "faststart",
            "-loglevel",
            "error",
            outputFile,
        ];

        var exitCode = await ExecuteFFmpegCommandAsync(args);
        if (exitCode == 0)
            return outputFile;

        if (File.Exists(outputFile))
            File.Delete(outputFile);

        return null;
    }

    private static async Task<string?> ExtractAudioCoverArt(UploadFile file)
    {
        var outputFile = Path.Combine(file.GetLocation(), $"{file.Id:N}_cover.webp");

        List<string> args =
        [
            "-y",
            "-i",
            file.GetThisFilePath(),
            "-map",
            "0:v:0",
            "-map_metadata",
            "-1",
            "-an",
            "-vf",
            "scale=512:512:force_original_aspect_ratio=decrease,pad=512:512:(ow-iw)/2:(oh-ih)/2,format=yuv420p",
            "-c:v",
            "libwebp",
            "-lossless",
            "0",
            "-compression_level",
            "3",
            "-qscale:v",
            "80",
            "-loglevel",
            "error",
            outputFile,
        ];

        var exitCode = await ExecuteFFmpegCommandAsync(args);
        if (exitCode == 0)
            return outputFile;

        if (File.Exists(outputFile))
            File.Delete(outputFile);

        return null;
    }

    private static async Task<int> ExecuteFFmpegCommandAsync(List<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi);
        if (process is null)
            return -1;

        await process.WaitForExitAsync();

        return process.ExitCode;
    }
}
