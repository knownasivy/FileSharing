using System.Text;
using FileSharing.ApiService.Extensions;
using FileSharing.ApiService.Services;

namespace FileSharing.ApiService.Features.Files;

public class GetFilesByFilePath
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("files/{b64FilePath}", Handler).WithTags("Files");
        }
    }
    
    public static async Task<IResult> Handler(
        ILogger<Endpoint> logger, 
        IWebHostEnvironment env,
        IUploadFileService uploadFileService, 
        string b64FilePath)
    {
        if (env.IsProduction()) return Results.Forbid();

        var filePathBytes = Convert.FromBase64String(b64FilePath);
        var filePath = Encoding.UTF8.GetString(filePathBytes);
        
        var files = await uploadFileService.GetAllByFilePathAsync(filePath);

        return Results.Ok(files);
    }
}