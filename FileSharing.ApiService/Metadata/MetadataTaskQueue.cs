using System.Threading.Channels;
using FileSharing.Constants;

namespace FileSharing.ApiService.Metadata;

public class MetadataTaskQueue : IMetadataTaskQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _queue;

    public MetadataTaskQueue()
    {
        var options = new BoundedChannelOptions(Misc.MetadataTaskCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<Func<CancellationToken, Task>>(options);
    }

    public async Task EnqueueAsync(Func<CancellationToken, Task> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _queue.Writer.WriteAsync(workItem);
    }
    
    public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }
}

public interface IMetadataTaskQueue
{
    Task EnqueueAsync(Func<CancellationToken, Task> workItem);
    Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}