using Amazon.S3;
using Amazon.S3.Model;
using FFMpegCore;
using FileSharing.ApiService.Files;
using FileSharing.Constants;
using ZLinq;

namespace FileSharing.ApiService.Metadata.Types;

public class AudioMetadata : IMetadata
{
    public Guid FileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    private string? AlbumArtist { get; set; }
    public bool AttachedPic { get; set; }
    private TimeSpan Duration { get; set; } = TimeSpan.Zero;
    
    private static readonly HashSet<string> KeysToKeep = ["title", "artist", "album", "album_artist"];
    
    public async Task<IMetadata?> ProcessAsync(FileUpload file, string filePath, IAmazonS3? s3)
    {
        if (s3 is null)
        {
            Console.WriteLine("s3 is null");
            return null;
        }
        if (file.Type != FileType.Audio) throw new Exception("Impossible");
        
        var mediaInfo = await FFProbe.AnalyseAsync(filePath);

        Duration = mediaInfo.Duration;
        FileId = file.Id;
        
        if (Duration > Storage.MaxFileDuration 
            || mediaInfo.ErrorData.Count != 0)
        {
            return null;
        }
        
        // TODO: Test this against files with more than one audio stream
        var videoStream = mediaInfo.PrimaryVideoStream;
        if (videoStream?.Disposition is not null)
        {
            if (videoStream.Disposition.TryGetValue("attached_pic", out var value))
            {
                AttachedPic = value;
            }
        }
    
        if (mediaInfo.Format.Tags is not null)
        {
            var tags = mediaInfo.Format.Tags;
            var filtered = tags.AsValueEnumerable()
                .Where(kv => KeysToKeep.Contains(kv.Key))
                .ToImmutableList();
            
            foreach (var kv in filtered)
            {
                var actions = new Dictionary<string, Action>
                {
                    ["title"] = () => Title = kv.Value,
                    ["artist"] = () => Artist = kv.Value,
                    ["album"] = () => Album = kv.Value,
                    ["album_artist"] = () => AlbumArtist = kv.Value
                };

                if (!actions.TryGetValue(kv.Key, out var action)) 
                    throw new Exception("Impossible");
        
                action();
            }
            
            if (Artist.Equals("") && AlbumArtist is not null)
            {
                Artist = AlbumArtist;
            }
        }
        
        var previewFileName = $"{file.Id:N}_prev.m4a";
        var coverFileName = $"{file.Id:N}_cover.webp";
        
        // TODO: Make list of task instead
        List<Task<string>> tasks = [ ConvertAudioToPreviewFileAsync(filePath) ];
        if (AttachedPic)
        {
            tasks.Add(ExtractAudioCoverArt(filePath, AttachedPic));
        }
        
        var results = await Task.WhenAll(tasks);
        
        try
        {
            var audioFile = results[0];
            var audio = new PutObjectRequest
            {
                BucketName = Storage.Bucket,
                Key = previewFileName,
                ContentType = "audio/mp4",
                DisablePayloadSigning = true
            };

            List<Task<PutObjectResponse>> uploads = [];
            
            if (string.IsNullOrEmpty(audioFile) || 
                !File.Exists(audioFile) ||
                new FileInfo(audioFile).Length > file.Size)
            {
                await using var inputStream = File.Open(filePath, FileMode.Open, FileAccess.Read);
                
                audio.InputStream = inputStream;

                await s3.PutObjectAsync(audio);
            }
            else
            {
                audio.FilePath = audioFile;
                uploads.Add(s3.PutObjectAsync(audio));
            }
            
            if (AttachedPic && results.Length != 1)
            {
                var coverFile = results[1];

                if (!string.IsNullOrEmpty(coverFile) && File.Exists(coverFile))
                {
                    var cover = new PutObjectRequest
                    {
                        BucketName = Storage.Bucket,
                        Key = coverFileName,
                        FilePath = coverFile,
                        ContentType = "image/webp",
                        DisablePayloadSigning = true
                    };
                
                    uploads.Add(s3.PutObjectAsync(cover));
                }
            }
            
            await Task.WhenAll(uploads);
        }
        finally
        {
            if (!string.IsNullOrEmpty(results[0]) && File.Exists(results[0]))
            {
                File.Delete(results[0]);
            }
            
            if (AttachedPic
                && results.Length != 1
                && !string.IsNullOrEmpty(results[0])
                && File.Exists(results[1]))
            {
                File.Delete(results[1]);
            }
        }
        
        return this;
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
    
    private static async Task<string> ExtractAudioCoverArt(string inputFilePath, bool picAttached)
    {
        // TODO: Feels messy
        if (!picAttached) return "";
        
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