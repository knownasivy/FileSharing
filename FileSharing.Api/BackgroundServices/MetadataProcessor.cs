using System.Threading.Channels;
using FileSharing.Api.Models;
using FileSharing.Api.Services;

namespace FileSharing.Api.BackgroundServices;

public interface IMetadataProcessor
{
    Task<bool> EnqueueAsync(UploadFile file, CancellationToken cancellationToken = default);
}

public class MetadataProcessor : BackgroundService, IMetadataProcessor
{
    private record MetadataItem(UploadFile File);
    
    private readonly IMetadataService _metadataService;
    private readonly ILogger<MetadataProcessor> _logger;
    private readonly Channel<MetadataItem> _channel;
    private readonly ChannelWriter<MetadataItem> _writer;
    private readonly int _maxWorkers = 1;
    
    public MetadataProcessor(IMetadataService metadataService, ILogger<MetadataProcessor> logger)
    {
        _metadataService = metadataService;
        _logger = logger;
        
        var options = new BoundedChannelOptions(250)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<MetadataItem>(options);
        _writer = _channel.Writer;
    }
    
    public async Task<bool> EnqueueAsync(UploadFile file, CancellationToken cancellationToken = default)
    {
        try
        {
            await _writer.WriteAsync(new MetadataItem(file), cancellationToken).ConfigureAwait(true);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _writer.Complete(); // Signal no more items
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = new List<Task>();
        for (var i = 0; i < _maxWorkers; i++)
        {
            tasks.Add(CompleteWorkerAsync(stoppingToken));
        }
        await Task.WhenAll(tasks);
    }

    private async Task CompleteWorkerAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                while (_channel.Reader.TryRead(out var request))
                {
                    try
                    {
                        // TODO: Pass cancellation token all the way through?
                        await _metadataService.HandlePreviewFileAsync(request.File);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing metadata for {FilePath}, FileId: {FileId}", 
                            request.File.FilePath, request.File.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Operation cancelled");
        }
    }
}