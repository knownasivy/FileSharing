using System.Diagnostics;
using System.IO.Hashing;
using Amazon.S3;
using Amazon.S3.Model;
using FastEndpoints;
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
            AddError("File too large.");
            await SendErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }
        
        var file = req.MapToFile();
        var result = await _fileService.CreateAsync(file);
            
        if (!Directory.Exists(result.GetLocation()))
            Directory.CreateDirectory(result.GetLocation());

        var newFileName = $"{result.Id:N}.{result.Extension}";
        var filePath = Path.Combine(result.GetLocation(), newFileName);

        var hasher = new XxHash3();

        // TODO: Check if file type matches mimetype
        await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[8192];
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
        if (result is null)
        {
            AddError("Impossible");
            await SendErrorsAsync(StatusCodes.Status500InternalServerError, ct);
            return;
        }

        if (result.Type == FileType.Audio)
        {
            // TODO: Move to a queue?
            _ = Task.Run(async () => await ProcessAudio(result, filePath), ct);
        }

        await SendAsync(result.MapToResponse(), cancellation: ct);
    }

    // TODO: Switch to FFMpegCore?
    private async Task ProcessAudio(FileUpload file, string filePath)
    {
        const string cmd = "ffmpeg";
                
        var previewFileName = $"{file.Id:N}_prev.m4a";
        var outputPath = Path.Combine(file.GetLocation(), previewFileName);
                
        var args = $"-i \"{filePath}\" -map_metadata -1 -vn -c:a aac -b:a 84k \"{outputPath}\" -y";
                
        var processStartInfo = new ProcessStartInfo
        {
            FileName = cmd,
            Arguments = args,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = processStartInfo;
        process.Start();
                
        var errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("FFmpeg cmd: {Ffmpeg} {Args}", cmd, args);
            _logger.LogError("FFmpeg error: {ErrorOutput}", errorOutput);
            return;
        }

        var size = new FileInfo(outputPath).Length;

        var uploadFile = size > file.Size ? filePath : outputPath;
        
        await using var fileStream = File.Open(uploadFile, FileMode.Open, FileAccess.Read);
        var request = new PutObjectRequest
        {
            BucketName = "files",
            Key = previewFileName,
            InputStream = fileStream,
            DisablePayloadSigning = true
        };
        
        await _s3.PutObjectAsync(request);
        File.Delete(outputPath);
    }
}