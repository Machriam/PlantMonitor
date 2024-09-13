using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.Unicode;
using Microsoft.AspNetCore.Mvc;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceConfiguration;

namespace Plantmonitor.Server.Features.Dashboard;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(IDataContext context, IEnvironmentConfiguration configuration, IWebHostEnvironment webHost)
{
    private const double InverseGigabyte = 1d / (1024d * 1024d * 1024d);
    private static readonly ConcurrentDictionary<string, DownloadInfo> s_fileReadyToDownload = new();
    public record struct DownloadInfo(long PhotoTourId, string Path, double CurrentSize, double SizeToDownloadInGb, bool ReadyToDownload);
    public record struct TemperatureSummaryData(string Device, IEnumerable<TemperatureDatum> Data);
    public record struct VirtualImageInfo(string Name, DateTime CreationDate);
    public record struct TemperatureDatum(DateTime Time, float Temperature, float Deviation);

    [HttpGet("virtualimagelist")]
    public IEnumerable<VirtualImageInfo> VirtualImageList(long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        return Directory.EnumerateFiles(configuration.VirtualImagePath(photoTour.Name, photoTour.Id))
            .Select(f => new VirtualImageInfo(Path.GetFileName(f), PhotoTourTrip.DateFromVirtualImage(f)));
    }

    [HttpGet("fullsummaryinformation")]
    public IEnumerable<VirtualImageSummary> SummaryForTour(long photoTourId)
    {
        return SummariesById(context, photoTourId)
            .ToList()
            .Where(s => s.ImageDescriptors.PlantDescriptors.Any() && !s.IsDark())
            .OrderBy(s => s.VirtualImageCreationDate);
    }

    [HttpPost("summaryexport")]
    public string CreatePhotoSummaryExport(long photoTourId)
    {
        var name = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId).Name;
        var resultJson = SummariesById(context, photoTourId)
            .ToList()
            .AsJson();
        var fileName = name.SanitizeFileName().Replace(" ", "") + ".json";
        var path = Path.GetTempPath() + fileName;
        File.WriteAllText(path, resultJson);
        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(path, fileName);
        }
        var downloadFilePath = DownloadFolder() + fileName + ".zip";
        File.WriteAllBytes(downloadFilePath, zipStream.ToArray());
        async Task DeleteFile()
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            File.Delete(downloadFilePath);
            File.Delete(path);
        }
        DeleteFile().RunInBackground(ex => ex.LogError());
        return Path.Combine(IWebHostEnvironmentExtensions.DownloadFolder, Path.GetFileName(downloadFilePath));
    }

    [HttpGet("temperaturedata")]
    public IEnumerable<TemperatureSummaryData> TemperatureSummary(long photoTourId)
    {
        return SummariesById(context, photoTourId).ToList()
            .SelectMany(vis => vis.ImageDescriptors.DeviceTemperatures.Select(dt => new { Temperature = dt, vis.ImageDescriptors.TripStart }))
            .GroupBy(dt => dt.Temperature.Name)
            .Select(g => new TemperatureSummaryData(g.Key,
                g.Select(dt => new TemperatureDatum(dt.TripStart, dt.Temperature.AverageTemperature, dt.Temperature.TemperatureDeviation))
                .OrderBy(dt => dt.Time)));
    }

    private static IQueryable<VirtualImageSummary> SummariesById(IDataContext context, long photoTourId)
    {
        var summaries = context.VirtualImageSummaryByPhotoTourIds
            .Where(vis => vis.PhotoTourId == photoTourId)
            .Select(vis => vis.Id)
            .ToHashSet();
        return context.VirtualImageSummaries
            .Where(vis => summaries.Contains(vis.Id));
    }

    [HttpGet("virtualimage")]
    public byte[] VirtualImage(string name, long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        var folder = configuration.VirtualImagePath(photoTour.Name, photoTour.Id);
        using var zip = ZipFile.Open(Path.Combine(folder, Path.GetFileName(name)), ZipArchiveMode.Read);
        var visPicture = zip.Entries.First(e => e.Name.Contains(PhotoTourTrip.VisPrefix));
        return visPicture.Open().ConvertToArray();
    }

    [HttpGet("statusofdownloadtourdata")]
    public IEnumerable<DownloadInfo> StatusOfDownloadTourData()
    {
        return s_fileReadyToDownload.Select(f =>
        {
            var path = webHost.DownloadFolderPath() + Path.GetFileName(f.Value.Path);
            if (!File.Exists(path)) return default;
            if (f.Value.ReadyToDownload) return f.Value;
            var currentSize = new FileInfo(path).Length * InverseGigabyte;
            return new DownloadInfo(f.Value.PhotoTourId, f.Value.Path, currentSize, f.Value.SizeToDownloadInGb, f.Value.ReadyToDownload);
        }).Where(f => f != default);
    }

    private string DownloadFolder()
    {
        var folder = webHost.DownloadFolderPath();
        Directory.CreateDirectory(folder);
        return folder;
    }

    [HttpPost("deletetourdata")]
    public void DeleteTourData(long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        var folder = configuration.VirtualImagePath(photoTour.Name, photoTour.Id);
        var zipFile = Path.Combine(DownloadFolder(), photoTour.Name.SanitizeFileName() + ".zip");
        if (File.Exists(zipFile)) File.Delete(zipFile);
    }

    [HttpGet("requestdownloadtourdata")]
    public DownloadInfo RequestDownloadTourData(long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        var folder = configuration.VirtualImagePath(photoTour.Name, photoTour.Id);
        var zipFile = Path.Combine(DownloadFolder(), photoTour.Name.SanitizeFileName() + ".zip");
        var sizeToDownload = new DirectoryInfo(folder).GetFiles()
            .Aggregate((0L), (a, f) => a += f.Length) * InverseGigabyte;
        var downloadFile = Path.Combine(IWebHostEnvironmentExtensions.DownloadFolder, Path.GetFileName(zipFile));
        var info = new DownloadInfo(photoTourId, downloadFile, 0d, sizeToDownload, false);
        async Task CreateAndDeleteZip()
        {
            await Task.Yield();
            s_fileReadyToDownload.Remove(zipFile, out _);
            if (File.Exists(zipFile)) File.Delete(zipFile);
            s_fileReadyToDownload.AddOrUpdate(zipFile, info, (_1, _2) => info);
            ZipFile.CreateFromDirectory(folder, zipFile, CompressionLevel.Fastest, true);
            if (s_fileReadyToDownload.TryGetValue(zipFile, out var currentInfo))
            {
                var size = new FileInfo(zipFile).Length * InverseGigabyte;
                var finalInfo = new DownloadInfo(currentInfo.PhotoTourId, currentInfo.Path, size, currentInfo.SizeToDownloadInGb, true);
                s_fileReadyToDownload.TryUpdate(zipFile, finalInfo, info);
            }
            await Task.Delay(TimeSpan.FromHours(12));
            File.Delete(zipFile);
            s_fileReadyToDownload.Remove(zipFile, out _);
        }
        CreateAndDeleteZip().RunInBackground(ex => ex.LogError());
        return info;
    }
}
