using Microsoft.Extensions.Logging.Abstractions;

namespace FileSharing.ApiService.Shared;

public class CountingStream : Stream
{
    private readonly Stream _innerStream;
    private readonly Action<int> _bytesReadCallback;
    private readonly ILogger<CountingStream> _logger;
    private bool _disposed;
    
    public CountingStream(Stream innerStream, Action<int> bytesReadUpdateCallback, ILogger<CountingStream>? logger = null)
    {
        _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        _bytesReadCallback = bytesReadUpdateCallback ?? throw new ArgumentNullException(nameof(bytesReadUpdateCallback));
        _logger = logger ?? NullLogger<CountingStream>.Instance;
    }
    
    private void UpdateBytesRead(int bytesRead)
    {
        if (bytesRead > 0) SafeInvokeCallback(bytesRead);
    }

    private void SafeInvokeCallback(int newBytes)
    {
        try
        {
            _bytesReadCallback(newBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bytes read callback for {BytesRead} bytes", newBytes);
        }
    }
    
    private void ThrowIfDisposed()
    {
        if (!_disposed) return;
        throw new ObjectDisposedException(nameof(CountingStream));
    }
    
    protected override void Dispose(bool disposing)
    {
        if (!disposing || _disposed) return;
        
        _innerStream.Dispose();
        _disposed = true;
        
        base.Dispose(disposing);
    }
    
    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _innerStream.DisposeAsync();
            _disposed = true;
        }
        
        GC.SuppressFinalize(this);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        var bytesRead = _innerStream.Read(buffer, offset, count);
        UpdateBytesRead(bytesRead);
        return bytesRead;
    }
    
    public override int ReadByte()
    {
        ThrowIfDisposed();
        var result = _innerStream.ReadByte();
        if (result != -1)
            UpdateBytesRead(1);
        return result;
    }
    
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var bytesRead = await _innerStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        UpdateBytesRead(bytesRead);
        return bytesRead;
    }
    
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var bytesRead = await _innerStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        UpdateBytesRead(bytesRead);
        return bytesRead;
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        ThrowIfDisposed();
        _innerStream.Write(buffer, offset, count);
    }
    
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _innerStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }
    
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _innerStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
    
    public override void Flush() 
    {
        ThrowIfDisposed();
        _innerStream.Flush();
    }
    
    public override Task FlushAsync(CancellationToken cancellationToken) 
    {
        ThrowIfDisposed();
        return _innerStream.FlushAsync(cancellationToken);
    }
    
    public override long Seek(long offset, SeekOrigin origin) 
    {
        ThrowIfDisposed();
        return _innerStream.Seek(offset, origin);
    }
    
    public override void SetLength(long value) 
    {
        ThrowIfDisposed();
        _innerStream.SetLength(value);
    }
    
    public override bool CanRead => !_disposed && _innerStream.CanRead;
    public override bool CanSeek => !_disposed && _innerStream.CanSeek;
    public override bool CanWrite => !_disposed && _innerStream.CanWrite;
    
    public override long Length 
    {
        get
        {
            ThrowIfDisposed();
            return _innerStream.Length;
        }
    }
    
    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return _innerStream.Position;
        }
        set
        {
            ThrowIfDisposed();
            _innerStream.Position = value;
        }
    }
}

