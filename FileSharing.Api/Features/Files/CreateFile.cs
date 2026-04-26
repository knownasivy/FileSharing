using System.IO.Hashing;
using FileSharing.Api.BackgroundServices;
using FileSharing.Api.Extensions;
using FileSharing.Api.Models;
using FileSharing.Api.Services;
using FileSharing.Api.Shared;
using FluentValidation;

namespace FileSharing.Api.Features.Files;

public static class CreateFile
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("site/files", Handler).WithTags("Files Site").DisableAntiforgery();
    }

    public record Response(string Id, long Size, string Name)
    {
        public static Response Create(UploadFile f) => new($"{f.Id:N}", f.Size, f.Name);
    }

    // TODO: Add Messages for errors
    public sealed class Validator : AbstractValidator<IFormFile>
    {
        public Validator()
        {
            RuleFor(f => f.Length).GreaterThan(0);
            RuleFor(f => f.Length).LessThanOrEqualTo(StorageConfig.MaxFileSize);

            RuleFor(f => f.FileName).NotEmpty();
            RuleFor(f => f.FileName.Length).LessThanOrEqualTo(250);
            RuleFor(f => f.FileName).Must(BeSecureFilename);

            // Audio files only right now
            RuleFor(f => FileUtil.GetFileType(f.FileName)).Must(type => type == FileType.Audio);
        }

        private static bool BeSecureFilename(string? filename)
        {
            if (string.IsNullOrEmpty(filename))
                return false;

            var fileName = Path.GetFileName(filename);

            return !string.IsNullOrEmpty(fileName)
                && fileName == filename
                && !Path.GetInvalidFileNameChars().Any(filename.Contains);
        }
    }

    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger,
        IValidator<IFormFile> validator,
        IUploadFileService uploadFileService,
        IMetadataProcessor metadataProcessor,
        IMetadataService metadataService,
        HttpContext context,
        IFormFile file,
        CancellationToken ct = default
    )
    {
        var validationResult = await validator.ValidateAsync(file, ct);

        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.Errors);

        var ip = context.Connection.RemoteIpAddress;
        if (ip is null)
            return Results.Problem("Could not determine IP address");

        var ipAddress = ip.MapToIPv4().ToString();
        var fileType = FileUtil.GetFileType(file.FileName);

        var uploadFile = await uploadFileService.CreateAsync(
            new UploadFile
            {
                Name = file.FileName,
                Size = file.Length,
                Type = fileType,
                IpAddress = ipAddress,
            }
        );

        if (uploadFile is null)
            return Results.InternalServerError();

        var uploadId = uploadFile.Id;
        var outputPath = uploadFile.GetThisFilePath();

        try
        {
            var bufferSize = FileUtil.GetBufferSize(file.Length);
            try
            {
                await using var fileStream = new FileStream(
                    outputPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: bufferSize,
                    useAsync: true
                );

                await file.CopyToAsync(fileStream, ct);
            }
            catch (OperationCanceledException)
            {
                await CleanUpFailure(uploadFileService, outputPath, uploadId);
                throw;
            }
            catch (Exception ex)
            {
                await CleanUpFailure(uploadFileService, outputPath, uploadId);
                return Results.Problem($"Upload failed: {ex.Message}");
            }

            var hasher = new XxHash3();
            await using var finalFs = new FileStream(
                outputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                useAsync: true
            );

            await hasher.AppendAsync(finalFs, ct);
            var hashBytes = hasher.GetCurrentHash();

            uploadFile = await uploadFileService.CompleteAsync(
                uploadFile,
                hashBytes,
                uploadFile.Size
            );
        }
        catch (Exception ex)
        {
            await CleanUpFailure(uploadFileService, outputPath, uploadId);
            logger.LogError(ex, "Error processing file upload for FileId: {FileId}", uploadId);
            return Results.Problem("An error occurred while processing the file");
        }

        if (uploadFile is null)
        {
            await CleanUpFailure(uploadFileService, outputPath, uploadId);
            logger.LogError("Upload file is null");
            return Results.Problem("An error occurred while processing the file");
        }

        if (uploadFile.FakeFile)
            return Results.Ok(Response.Create(uploadFile));
        if (uploadFile.Type != FileType.Audio)
            return Results.BadRequest();

        if (uploadFile.Extension is "m4a" or "mp3")
        {
            var previewFile = await MetadataService.ConvertAudioToPreviewFileFastAsync(uploadFile);

            if (previewFile is null)
            {
                await CleanUpFailure(uploadFileService, outputPath, uploadId);
                return Results.Problem("An error occurred while processing the preview file");
            }
        }

        var success = await metadataService.ProcessAudioFileAsync(uploadFile);
        if (!success)
            return Results.InternalServerError("Error creating preview file");

        await metadataProcessor.EnqueueAsync(uploadFile, ct);

        logger.LogInformation("New File Uploaded: {FileName}", uploadFile.Name);

        return Results.Ok(Response.Create(uploadFile));
    }

    // TODO: Move functions below to their respective services?
    private static async Task CleanUpFailure(
        IUploadFileService uploadFileService,
        string outputPath,
        Guid uploadId
    )
    {
        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            await uploadFileService.DeleteByIdAsync(uploadId);
        }
        catch
        { /* ignored */
        }
    }
}
