using System.Text.Json;
using FFMpegCore;
using ZLinq;

// Analyze the media file

var options = new JsonSerializerOptions
{
    WriteIndented = true
};

var test1 = await GetAudioInfo("song.m4a");
var test2 = await GetAudioInfo("song.mp3");

GetAudioImage(test1);
GetAudioImage(test2);

PrintTrack(test1);
PrintTrack(test2);


void GetAudioImage(AudioFileInfo? fileInfo)
{
    if (fileInfo is null || !fileInfo.AttachedPic)
    {
        Console.WriteLine("No pic attached");
        return;
    }
    
    var outputFile = $"{Path.GetFileNameWithoutExtension(fileInfo.FilePath)}.jpg";
    Console.WriteLine($"Output file: {outputFile}");
    
    FFMpegArguments
        .FromFileInput(fileInfo.FilePath)
        .OutputToFile(outputFile, overwrite: true, o =>
        {
            o.WithCustomArgument("-an");
            o.WithCustomArgument("-vcodec copy");
        })
        .ProcessSynchronously();
}

async Task<AudioFileInfo?> GetAudioInfo(string fileName)
{
    var basePath = AppContext.BaseDirectory;
    var filePath = Path.Combine(basePath, "Resources", fileName);
    var mediaInfo = await FFProbe.AnalyseAsync(filePath);
    
    var audioFile = new AudioFileInfo
    {
        FileName = fileName,
        Duration = mediaInfo.Duration,
        Previewable = mediaInfo.Duration <= TimeSpan.FromMinutes(45) && mediaInfo.ErrorData.Count == 0,
        AttachedPic = false,
        FilePath = filePath
    };

    var keysToKeep = new HashSet<string> { "title", "artist", "album", "album_artist" };

    if (mediaInfo.Format.Tags is null) return audioFile;
    
    var videoStream = mediaInfo.PrimaryVideoStream;
    if (videoStream?.Disposition is not null)
    {
        if (videoStream.Disposition.TryGetValue("attached_pic", out var value))
        {
            audioFile.AttachedPic = value;
        }
    }
    
    var tags = mediaInfo.Format.Tags;
    var filtered = tags.AsValueEnumerable()
        .Where(kv => keysToKeep.Contains(kv.Key))
        .ToImmutableList();

    foreach (var kv in filtered)
    {
        var actions = new Dictionary<string, Action>
        {
            ["title"] = () => audioFile.Title = kv.Value,
            ["artist"] = () => audioFile.Artist = kv.Value,
            ["album"] = () => audioFile.Album = kv.Value,
            ["album_artist"] = () => audioFile.Artist ??= kv.Value
        };

        if (!actions.TryGetValue(kv.Key, out var action)) 
            throw new Exception("Impossible");
        
        action();
    }
    
    return audioFile;
}

void PrintTrack(AudioFileInfo? fileInfo)
{
    if (fileInfo is null)
    {
        Console.WriteLine("Track is null");
    }
    
    var jsonString = JsonSerializer.Serialize(fileInfo, options);
    Console.WriteLine(jsonString);
}

public class AudioFileInfo
{
    public required string FileName { get; init; }
    public string? Title { get; set; }
    public string? Album { get; set; }
    public string? Artist { get; set; }
    public required TimeSpan Duration { get; init; }
    public required bool Previewable { get; set; }
    public required bool AttachedPic { get; set; }
    public required string FilePath { get; init; }
}