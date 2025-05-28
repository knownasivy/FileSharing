using System.Diagnostics;
using System.IO.Hashing;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
using FFMpegCore;
using FileSharing.ApiService.Contracts;
using FileSharing.ApiService.Contracts.Requests;
using FileSharing.ApiService.Contracts.Responses;
using FileSharing.Constants;

namespace FileSharing.ApiService.Files.Endpoints;

public class CreateFileEndpoint : Endpoint<CreateFileRequest, FileResponse>
{
    private readonly IFileService _fileService;
    private readonly IAmazonS3 _s3;
    private readonly ILogger<CreateFileEndpoint> _logger;

    public CreateFileEndpoint(IFileService fileService, IAmazonS3 s3, ILogger<CreateFileEndpoint> logger)
    {
        _fileService = fileService;
        _s3 = s3;
        _logger = logger;
    }
    
    public override void Configure()
    {
        // TODO: Think about groups
        Post("/file/upload");
        AllowFileUploads();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateFileRequest req, CancellationToken ct)
    {
        // TODO: Have i protected myself against the stampede threat? 
        
        switch (req.File.Length)
        {
            case 0:
                AddError("One file must be provided.");
                await SendErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            case > Limits.MaxFileSize:
                AddError("File too large.");
                await SendErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
        }
        
        var fileName = req.File.FileName;
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
        {
            AddError("Unsupported file format.");
            await SendErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }
        
        var file = req.MapToFile();
        if (file.Type == FileType.Unknown)
        {
            AddError("Unsupported file format.");
            await SendErrorsAsync(StatusCodes.Status400BadRequest, ct);
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
            var buffer = new byte[Rates.Chunks];
            int bytesRead;
            
            await using var inputStream = req.File.OpenReadStream();
            while ((bytesRead = await inputStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                hasher.Append(buffer.AsSpan(0, bytesRead));
            }
        }
            
        var hashBytes = hasher.GetCurrentHash();
        
        result = await _fileService.CompleteAsync(result.Id, hashBytes, filePath);
        switch (result)
        {
            case null:
                AddError("Impossible");
                await SendErrorsAsync(StatusCodes.Status500InternalServerError, ct);
                return;
            case { FakeFile: false, Type: FileType.Audio }:
                // TODO: Move to a queue?
                _ = Task.Run(async () => await ProcessAudio(result, filePath), ct);
                break;
        }

        await SendAsync(result.MapToResponse(), cancellation: ct);
    }

    private static async Task<string> ConvertToAudioPreviewFileAsync(string inputFilePath)
    {
        // Generate a unique temporary file path for the preview
        var tempPreviewPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.m4a");

        await FFMpegArguments
            .FromFileInput(inputFilePath)
            .OutputToFile(tempPreviewPath, overwrite: true, options => options
                    .WithAudioCodec("aac")
                    .WithAudioBitrate(80)
                    .ForceFormat("ipod")
                    .WithCustomArgument("-vn") // Disable video
                    .WithCustomArgument("-map_metadata")
                    .WithCustomArgument("-1")  // Remove metadata
            )
            .ProcessAsynchronously();

        return tempPreviewPath;
    }

    // TODO: Switch to FFMpegCore?
    private async Task ProcessAudio(FileUpload file, string filePath)
    {
        /*
         * TODO:
         * 1. Make sure audio isnt too long
         * 2. Make sure it has one audio stream
         * 3. Extract metadata
         */
        
        //_logger.LogInformation("Processing audio file...");
        
        var previewFileName = $"{file.Id:N}_prev.m4a";
        var tmpFile = await ConvertToAudioPreviewFileAsync(filePath);

        try
        {
            var request = new PutObjectRequest
            {
                BucketName = "files",
                Key = previewFileName,
                FilePath = tmpFile,
                DisablePayloadSigning = true
            };

            if (!File.Exists(tmpFile) || new FileInfo(tmpFile).Length > file.Size)
            {
                request.InputStream = File.Open(filePath, FileMode.Open, FileAccess.Read);
            }

            await _s3.PutObjectAsync(request);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to r2");
        }
        finally
        {
            if (File.Exists(tmpFile))
            {
                File.Delete(tmpFile);
                _logger.LogInformation("Deleted temp file: {TmpFile}", tmpFile);
            }
        }
    }
}