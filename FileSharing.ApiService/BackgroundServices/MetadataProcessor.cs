using System.Threading.Channels;
using FileSharing.ApiService.Models;
using FileSharing.ApiService.Services;

namespace FileSharing.ApiService.BackgroundServices;



public interface IMetadataProcessor
{
    Task<bool> EnqueueAsync(UploadFile file, string filePath, CancellationToken cancellationToken = default);
}

public class MetadataProcessor : BackgroundService, IMetadataProcessor
{
    private record MetadataItem(UploadFile File, string FilePath);
    
    private readonly IMetadataService _metadataService;
    private readonly ILogger<MetadataProcessor> _logger;
    private readonly Channel<MetadataItem> _channel;
    private readonly ChannelWriter<MetadataItem> _writer;
    private readonly int _maxWorkers = Environment.ProcessorCount;
    
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
    
    public async Task<bool> EnqueueAsync(UploadFile file, string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await _writer.WriteAsync(new MetadataItem(file, filePath), cancellationToken);
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
                        // TODO: Pass cancellation token all the way through
                        await _metadataService.ProcessFile(request.File, request.FilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing metadata for {FilePath}, FileId: {FileId}", 
                            request.FilePath, request.File.Id);
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