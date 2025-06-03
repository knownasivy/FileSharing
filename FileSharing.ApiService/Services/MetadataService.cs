using System.Collections.Immutable;
using Amazon.S3.Model;
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
    
    Task<bool> ProcessFile(UploadFile file, string filePath);
    
    Task<AudioMetadata?> CreateAudioMetadata(UploadFile file, string filePath);
    Task<AudioMetadata?> GetAudioMetadataByFileId(Guid fileId);
    
    Task<ZipMetadata?> CreateZipMetadata(UploadFile file, string filePath);
    Task<ZipMetadata?> GetZipMetadataByFileId(Guid fileId);

    // Not really metadata
    Task<bool> ProcessImageFile(UploadFile file, string filePath);
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

    public async Task<bool> ProcessFile(UploadFile file, string filePath)
    {
        if (file.Status != FileStatus.Uploaded) throw new Exception("File not uploaded");
        
        if (file.Type == FileType.Archive &&
            !file.Extension.Equals("zip", StringComparison.OrdinalIgnoreCase)) return true;

        if (!file.CanExtractMetadata()) return true;
        
        try
        {
            switch (file.Type)
            {
                case FileType.Audio:
                {
                    var metadata = await CreateAudioMetadata(file, filePath);
                    if (metadata is null) return false;
                    
                    await using var connection = await _dataSource.OpenConnectionAsync();
            
                    await connection.ExecuteAsync(
                        """
                        INSERT INTO AudioMetadata (FileId, Title, Album, Artist, AttachedPic)
                        VALUES (@FileId, @Title, @Album, @Artist, @AttachedPic)
                        """, metadata);
                    return true;
                }
                case FileType.Archive:
                {
                    var metadata = await CreateZipMetadata(file, filePath);
                    if (metadata is null) return false;
                    
                    await using var connection = await _dataSource.OpenConnectionAsync();
            
                    await connection.ExecuteAsync(
                        """
                        INSERT INTO ZipMetadata (FileId, Files, Password)
                        VALUES (@FileId, @Files, @Password)
                        """, metadata);
                    return true;
                }
                case FileType.Image:
                    await ProcessImageFile(file, filePath);
                    return true;
                case FileType.Unsupported:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file");
        }
        
        return false;
    }

    public async Task<AudioMetadata?> CreateAudioMetadata(UploadFile file, string filePath)
    {
        if (file.Type != FileType.Audio) throw new Exception("Impossible");
        
        var mediaInfo = await FFProbe.AnalyseAsync(filePath);
        if (mediaInfo.Duration > Storage.MaxFileDuration 
            || mediaInfo.ErrorData.Count != 0)
        {
            return null;
        }
        
        var metadata = new AudioMetadata { FileId = file.Id };
        
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
        var audioFile = await ConvertAudioToPreviewFileAsync(filePath);
        
        try
        {
            if (string.IsNullOrEmpty(audioFile) || 
                !File.Exists(audioFile) ||
                new FileInfo(audioFile).Length > file.Size)
            {
                await using var inputStream = File.Open(filePath, FileMode.Open, FileAccess.Read);

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
        var coverFile = await ExtractAudioCoverArt(filePath);
        
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
        var query = connection.SqlBuilder($"SELECT * FROM AudioMetadata WHERE FileId = {fileId} LIMIT 1");
        return await query.QueryFirstOrDefaultAsync<AudioMetadata>();
    }

    public async Task<ZipMetadata?> CreateZipMetadata(UploadFile file, string filePath)
    {
        if (file.Type != FileType.Archive) throw new Exception("Impossible");
        if (file.Extension != "zip") return null;

        var metadata = new ZipMetadata { FileId = file.Id };
        
        await using var stream = File.OpenRead(filePath);
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
        var query = connection.SqlBuilder($"SELECT * FROM ZipMetadata WHERE FileId = {fileId} LIMIT 1");
        return await query.QueryFirstOrDefaultAsync<ZipMetadata>();
    }

    public Task<bool> ProcessImageFile(UploadFile file, string filePath)
    {
        throw new NotImplementedException();
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
