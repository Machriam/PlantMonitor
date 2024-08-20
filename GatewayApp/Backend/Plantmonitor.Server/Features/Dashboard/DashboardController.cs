using System.Collections.Concurrent;
using System.IO.Compression;
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

    [HttpGet("virtualimagelist")]
    public IEnumerable<string> VirtualImageList(long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        return Directory.EnumerateFiles(configuration.VirtualImagePath(photoTour.Name, photoTour.Id))
            .Select(f => Path.GetFileName(f));
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
            await Task.Delay(TimeSpan.FromMinutes(30));
            File.Delete(zipFile);
            s_fileReadyToDownload.Remove(zipFile, out _);
        }
        CreateAndDeleteZip().RunInBackground(ex => ex.LogError());
        return info;
    }
}
