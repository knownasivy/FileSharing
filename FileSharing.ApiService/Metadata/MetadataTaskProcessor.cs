namespace FileSharing.ApiService.Metadata;

public class MetadataTaskProcessor : BackgroundService
{
    private readonly IMetadataTaskQueue _taskQueue;
    private readonly ILogger<MetadataTaskProcessor> _logger;

    public MetadataTaskProcessor(IMetadataTaskQueue taskQueue, ILogger<MetadataTaskProcessor> logger)
    {
        _taskQueue = taskQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(token);
                await workItem(token);
                _logger.LogInformation("Work item processed");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in metadata task processor.");
            }
        }
    }
}