using System.Collections.Immutable;
using Dapper;
using FFMpegCore;
using FileSharing.ApiService.Models;
using FileSharing.Constants;
using ICSharpCode.SharpZipLib.Zip;
using InterpolatedSql.Dapper;
using Npgsql;
using ZLinq;

namespace FileSharing.ApiService.Services;

public interface IMetadataService
{
    // TODO: Better name?
    // Task<IMetadata?> CreateAsync(FileUpload file, string filePath);
    // Task<IMetadata?> GetByFileAsync(FileUpload file);
    
    Task<bool> ProcessFile(UploadFile file);
    
    Task<AudioMetadata?> CreateAudioMetadata(UploadFile file);
    Task<AudioMetadata?> GetAudioMetadataByFileId(Guid fileId);
    
    Task<ZipMetadata?> CreateZipMetadata(UploadFile file);
    Task<ZipMetadata?> GetZipMetadataByFileId(Guid fileId);
}

public class MetadataService : IMetadataService
{
    private static readonly ImmutableHashSet<string> AudioKeysToKeep = ["title", "artist", "album", "album_artist"];
    
    private readonly ICloudService _cloudService;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<MetadataService> _logger;

    public MetadataService(ICloudService cloudService, NpgsqlDataSource dataSource, ILogger<MetadataService> logger)
    {
        _cloudService = cloudService;
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<bool> ProcessFile(UploadFile file)
    {
        if (file.Status != FileStatus.Uploaded) throw new Exception("File not uploaded");
        if (file.Hash.Length == 0) throw new Exception("File has no hash");
        
        if (file.Type == FileType.Archive &&
            !file.Extension.Equals("zip", StringComparison.OrdinalIgnoreCase)) return true;

        if (!file.CanExtractMetadata()) return true;
        
        try
        {
            switch (file.Type)
            {
                case FileType.Audio:
                {
                    var metadata = await CreateAudioMetadata(file);
                    if (metadata is null) return false;
                    
                    await using var connection = await _dataSource.OpenConnectionAsync();
            
                    await connection.ExecuteAsync(
                        """
                        INSERT INTO AudioMetadata (FileHash, Title, Album, Artist, AttachedPic)
                        VALUES (@FileHash, @Title, @Album, @Artist, @AttachedPic)
                        """, metadata);
                    return true;
                }
                case FileType.Archive:
                {
                    var metadata = await CreateZipMetadata(file);
                    if (metadata is null) return false;
                    
                    await using var connection = await _dataSource.OpenConnectionAsync();
            
                    await connection.ExecuteAsync(
                        """
                        INSERT INTO ZipMetadata (FileHash, Files, Password)
                        VALUES (@FileHash, @Files, @Password)
                        """, metadata);
                    return true;
                }
                case FileType.Unsupported: break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file");
        }
        
        return false;
    }

    public async Task<AudioMetadata?> CreateAudioMetadata(UploadFile file)
    {
        if (file.Type != FileType.Audio) throw new Exception("Impossible");

        var mediaInfo = await FFProbe.AnalyseAsync(file.GetThisFilePath());
        if (mediaInfo.Duration > Storage.MaxAudioFileProcessDuration 
            || mediaInfo.ErrorData.Count != 0)
        {
            return null;
        }
        
        var metadata = new AudioMetadata { FileHash = file.Hash };
        
        var videoStream = mediaInfo.PrimaryVideoStream;
        if (videoStream?.Disposition is not null &&
            videoStream.Disposition.TryGetValue("attached_pic", out var value))
        {
            metadata.AttachedPic = value;
        }
        
        if (mediaInfo.Format.Tags is not null)
        {
            var tags = mediaInfo.Format.Tags;
            var filtered = tags.AsValueEnumerable()
                .Where(kv => AudioKeysToKeep.Contains(kv.Key))
                .ToImmutableList();
            
            string? albumArtist = null;
            foreach (var kv in filtered)
            {
                var actions = new Dictionary<string, Action>
                {
                    ["title"] = () => metadata.Title = kv.Value,
                    ["artist"] = () => metadata.Artist = kv.Value,
                    ["album"] = () => metadata.Album = kv.Value,
                    ["album_artist"] = () => albumArtist = kv.Value
                };

                if (!actions.TryGetValue(kv.Key, out var action)) 
                    throw new Exception("Impossible");
        
                action();
            }
            
            if (string.IsNullOrWhiteSpace(metadata.Artist) 
                && !string.IsNullOrWhiteSpace(albumArtist))
            {
                metadata.Artist = albumArtist;
            }
        }
        
        var previewFileName = $"{file.Id:N}_prev.m4a";
        var audioFile = await ConvertAudioToPreviewFileAsync(file.GetThisFilePath());
        
        try
        {
            if (string.IsNullOrEmpty(audioFile) || 
                !File.Exists(audioFile) ||
                new FileInfo(audioFile).Length > file.Size)
            {
                await using var inputStream = File.Open(file.GetThisFilePath(), FileMode.Open, FileAccess.Read);

                await _cloudService.UploadAsync(previewFileName, inputStream, "audio/mp4");
            }
            else
            {
                await _cloudService.UploadAsync(previewFileName, audioFile, "audio/mp4");
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(audioFile) && File.Exists(audioFile))
            {
                File.Delete(audioFile);
            }
        }
        
        if (!metadata.AttachedPic) return metadata;
        
        var coverFileName = $"{file.Id:N}_cover.webp";
        var coverFile = await ExtractAudioCoverArt(file.GetThisFilePath());
        
        try
        {
            if (!string.IsNullOrEmpty(coverFile) && File.Exists(coverFile))
            {
                await _cloudService.UploadAsync(coverFileName, coverFile, "image/webp");
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(audioFile) && File.Exists(audioFile))
            {
                File.Delete(audioFile);
            }
        }
        
        return metadata;
    }

    public async Task<AudioMetadata?> GetAudioMetadataByFileId(Guid fileId)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder(
            $"""
             SELECT audio.*
             FROM UploadFiles file
                 JOIN AudioMetadata audio ON audio.FileHash = file.Hash
             WHERE file.Id = {fileId}
             LIMIT 1
             """);

        return await query.QueryFirstOrDefaultAsync<AudioMetadata>();
    }

    public async Task<ZipMetadata?> CreateZipMetadata(UploadFile file)
    {
        if (file.Type != FileType.Archive) throw new Exception("Impossible");
        if (file.Extension != "zip") return null;

        var metadata = new ZipMetadata { FileHash = file.Hash };
        
        await using var stream = File.OpenRead(file.GetThisFilePath());
        using var zipFile = new ZipFile(stream);
        
        foreach (ZipEntry entry in zipFile)
        {
            metadata.Files.Add(new ZipItem(entry.Name, entry.Size));
            if (!metadata.Password && entry.IsCrypted)
                metadata.Password = true;
        }
        
        return metadata;
    }

    public async Task<ZipMetadata?> GetZipMetadataByFileId(Guid fileId)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        var query = connection.SqlBuilder(
            $"""
             SELECT zip.*
             FROM UploadFiles file
                 JOIN ZipMetadata zip ON zip.FileHash = file.Hash
             WHERE file.Id = {fileId}
             LIMIT 1
             """);

        return await query.QueryFirstOrDefaultAsync<ZipMetadata>();
    }

    private static async Task<string> ConvertAudioToPreviewFileAsync(string inputFilePath)
    {
        var tempPreviewPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.m4a");

        await FFMpegArguments
            .FromFileInput(inputFilePath)
            .OutputToFile(tempPreviewPath, overwrite: true, o =>
            {
                o.WithAudioCodec("aac");
                o.WithAudioBitrate(80);
                o.ForceFormat("ipod");
                o.WithCustomArgument("-vn") // Disable video
                    .WithCustomArgument("-map_metadata")
                    .WithCustomArgument("-1"); // Remove metadata
            })
            .ProcessAsynchronously();

        return tempPreviewPath;
    }
    
    private static async Task<string> ExtractAudioCoverArt(string inputFilePath)
    {
        var tempPreviewPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.webp");
        
        // TODO: Do i need to resize img too?
        await FFMpegArguments
            .FromFileInput(inputFilePath)
            .OutputToFile(tempPreviewPath, overwrite: true, o =>
            {
                o.WithVideoCodec("libwebp");
                o.ForcePixelFormat("yuva420p");
                o.WithFrameOutputCount(1);
                o.WithCustomArgument("-an");
            })
            .ProcessAsynchronously();
        
        return tempPreviewPath;
    }
}

// TODO: Might not need this
