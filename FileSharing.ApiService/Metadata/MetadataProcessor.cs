using System.Threading.Channels;
using FileSharing.ApiService.Files;

namespace FileSharing.ApiService.Metadata;

public class MetadataProcessor : BackgroundService
{
    private readonly IMetadataService _metadataService;
    private readonly ILogger<MetadataProcessor> _logger;
    private readonly Channel<MetadataItem> _channel;
    
    public MetadataProcessor(
        IMetadataService metadataService, 
        ILogger<MetadataProcessor> logger, 
        Channel<MetadataItem> channel)
    {
        _metadataService = metadataService;
        _logger = logger;
        _channel = channel;
    }

    // TODO: Run more than one task at a time
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _channel.Reader.WaitToReadAsync(stoppingToken))
        {
            var request = await _channel.Reader.ReadAsync(stoppingToken);

            await _metadataService.CreateAsync(request.FileUpload, request.FilePath);
        }
    }
}

public record MetadataItem(FileUpload FileUpload, string FilePath);