using FileSharing.ApiService.Contracts.Requests;
using FileSharing.ApiService.Contracts.Responses;
using FileSharing.ApiService.Files;
using FileSharing.ApiService.Util;

namespace FileSharing.ApiService.Contracts;

public static class ContractMapping
{
    public static FileUpload MapToFile(this CreateFileRequest request)
    {
        var fileName = request.File.FileName;
        
        // TODO: Check file type matches actual filetype
        return new FileUpload
        {
            Id = Guid.NewGuid(),
            Name = fileName,
            Size = (int)request.File.Length, // TODO: I think int is fine?
            Type = FileUtil.GetFileType(fileName),
            FakeFile = false
        };
    }

    public static FileResponse MapToResponse(this FileUpload file)
    {
        return new FileResponse
        {
            Id = $"{file.Id:N}",
            Name = file.Name,
            Size = file.Size,
            Type = file.Type.ToString(),
            Status = file.Status.ToString(),
            CreatedAt = file.CreatedFormated
            //Hash = file.Hash
        };
    }
}