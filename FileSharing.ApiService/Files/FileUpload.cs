﻿using System.Net;
using FileSharing.ApiService.Util;
using ZLinq;

namespace FileSharing.ApiService.Files;

public class FileUpload
{
    public required Guid Id { get; init; }
    
    public required string Name { get; set; }
    
    // In bytes
    public required int Size { get; init; }

    public required FileType Type { get; init; }

    public FileStatus Status { get; set; } = FileStatus.Uploading;
    
    public DateTime CreatedAt => DateTime.UtcNow;
    
    // TODO: Might make mistakes with this later
    // When set to -> []
    public byte[] Hash { get; set; } = [];
    
    public required bool FakeFile { get; set; }
    
    public required string IpAddress { get; init; }

    public string CreatedFormated => 
        CreatedAt.ToString("MM-dd-yy");

    public string Extension => FileUtil.GetExtension(Name);
    
    public string GetLocation()
    {
        // If i keep using this check move to func
        if (FakeFile)
            throw new Exception("Impossible");
        return Path.Combine("Uploads", CreatedFormated);
    }
    
    // TODO: Might be able to use more than one place
    public string GetPreviewFilename()
    {
        if (FakeFile) throw new Exception("Fake file doesnt have preview");
        
        return Type switch
        {
            FileType.Audio => $"{Id:N}_prev.m4a",
            FileType.Image => $"{Id:N}_img.webp",
            _ => throw new Exception("Impossible")
        };

    }

    public bool HashEquals(byte[] hash) => 
        hash.AsValueEnumerable().SequenceEqual(hash);
    
    public bool HashEquals(FileUpload other) => 
        HashEquals(other.Hash);
}

public enum FileType
{
    Audio,
    Archive,
    Image,
    Unknown
}

public enum FileStatus
{
    Uploading,
    Uploaded
    //Deleted
}