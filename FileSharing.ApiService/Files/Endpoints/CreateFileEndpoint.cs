using System.Diagnostics;
using System.IO.Hashing;
using System.Threading.Channels;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
using FFMpegCore;
using FileSharing.ApiService.Contracts;
using FileSharing.ApiService.Contracts.Requests;
using FileSharing.ApiService.Contracts.Responses;
using FileSharing.ApiService.Metadata;
using FileSharing.Constants;

namespace FileSharing.ApiService.Files.Endpoints;

public class CreateFileEndpoint : Endpoint<CreateFileRequest, FileResponse>
{
    private readonly IFileService _fileService;
    // private readonly IMetadataService _metadataService;
    // private readonly IMetadataTaskQueue _taskQueue;
    private readonly Channel<MetadataItem> _channel;
    private readonly ILogger<CreateFileEndpoint> _logger;

    public CreateFileEndpoint(
        IFileService fileService,
        // IMetadataService metadataService,
        // IMetadataTaskQueue taskQueue,
        Channel<MetadataItem> channel,
        ILogger<CreateFileEndpoint> logger)
    {
        _fileService = fileService;
        _channel = channel;
        // _metadataService = metadataService;
        // _taskQueue = taskQueue;
        _logger = logger;
    }
    
    public override void Configure()
    {
        // TODO: Think about groups
        Post("/file/upload");
        AllowFileUploads();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateFileRequest req, CancellationToken token)
    {
        // TODO: Have i protected myself against the stampede threat? 
        
        switch (req.File.Length)
        {
            case 0:
                AddError("One file must be provided.");
                await SendErrorsAsync(StatusCodes.Status400BadRequest, token);
                return;
            case > Storage.MaxFileSize:
                AddError("File too large.");
                await SendErrorsAsync(StatusCodes.Status400BadRequest, token);
                return;
        }
        
        var fileName = req.File.FileName;
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
        {
            AddError("Unsupported file format.");
            await SendErrorsAsync(StatusCodes.Status400BadRequest, token);
            return;
        }
        
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (ipAddress is null)
        {
            _logger.LogError("Unable to get ip address for {FileName}.", fileName);
            ipAddress = "127.0.0.1";
        }
        
        var file = req.MapToFile(ipAddress);
        if (file.Type == FileType.Unknown)
        {
            AddError("Unsupported file format.");
            await SendErrorsAsync(StatusCodes.Status400BadRequest, token);
            return;
        }
        
        var result = await _fileService.CreateAsync(file);
            
        if (!Directory.Exists(result.GetLocation()))
            Directory.CreateDirectory(result.GetLocation());

        var newFileName = $"{result.Id:N}.{result.Extension}";
        var filePath = Path.Combine(result.GetLocation(), newFileName);

        var hasher = new XxHash3();

        // TODO: Check if file type matches mimetype
        await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[Storage.BufferSize];
            int bytesRead;
            
            await using var inputStream = req.File.OpenReadStream();
            while ((bytesRead = await inputStream.ReadAsync(buffer, token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                hasher.Append(buffer.AsSpan(0, bytesRead));
            }
        }
            
        var hashBytes = hasher.GetCurrentHash();
        
        result = await _fileService.CompleteAsync(result.Id, hashBytes, filePath);
        if (result is null)
        {
            await SendErrorsAsync(StatusCodes.Status500InternalServerError, token);
            return;
        }

        if (!result.FakeFile)
        {
            await _channel.Writer.WriteAsync(new MetadataItem(result, filePath), token);
        }

        await SendAsync(result.MapToResponse(), cancellation: token);
    }
}