namespace FileSharing.Api.Shared;


public static class BytesSize
{
    public const int KiB = 1024;
    public const int MiB = 1024 * 1024;
    public const long GiB = 1024 * 1024 * 1024;
    
    public static long FromKb(long amount)
    {
        return amount * KiB;
    }
    
    public static long FromMb(long amount)
    {
        return amount * MiB;
    }
    
    public static long FromGb(long amount)
    {
        return amount * GiB;
    }
}

public readonly struct BytesSizeConvert(long bytes)
{
    
    private static readonly Dictionary<string, long> Suffixes = new()
    {
        ["B"] = 1,
        ["KB"] = BytesSize.KiB,
        ["MB"] = BytesSize.MiB,
        ["GB"] = BytesSize.GiB
    };
    
    public long Bytes { get; }

    public static bool TryParse(string input, out BytesSizeConvert result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        input = input.Trim().ToUpperInvariant();
        foreach (var suffix in Suffixes)
        {
            if (!input.EndsWith(suffix.Key)) continue;
            
            var numberPart = input[..^suffix.Key.Length];
            if (!long.TryParse(numberPart, out var number)) continue;
            
            result = new BytesSizeConvert(number * suffix.Value);
            return true;
        }
        return false;
    }
}