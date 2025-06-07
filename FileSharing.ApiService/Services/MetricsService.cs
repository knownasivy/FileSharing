using System.Diagnostics.Metrics;

namespace FileSharing.ApiService.Services;

public interface IMetricsService
{
    void RecordDownload(string ipAddress, long bytesRead);
}

public class MetricsService : IMetricsService
{
    private readonly ILogger<DownloadService> _logger;

    public MetricsService(ILogger<DownloadService> logger)
    {
        _logger = logger;
    }

    private static readonly Meter Meter = new("FileSharing.ApiService", "1.0.0");
    private static readonly Counter<long> DownloadedBytesCounter =
        Meter.CreateCounter<long>("downloaded_bytes_total");

    public void RecordDownload(string ipAddress, long bytesRead)
    {
        _logger.LogInformation("Added new metric: ({ip}, {bytes})", ipAddress, bytesRead);
        DownloadedBytesCounter.Add(bytesRead, new KeyValuePair<string, object?>("ip_address", ipAddress));
    }
}