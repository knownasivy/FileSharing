namespace FileSharing.ApiService.Downloads;

public class Download : IAsyncDisposable, IDisposable
{
    public required Stream DownloadStream { get; init; }
    public required string FileName { get; init; }
    
    private bool _disposed = false;
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await DownloadStream.DisposeAsync();
            _disposed = true;
        }
        
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DownloadStream.Dispose();
            _disposed = true;
        }
        
        GC.SuppressFinalize(this);
    }
}