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

    [HttpGet("downloadtourdata")]
    public string DownloadTourData(long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        var folder = configuration.VirtualImagePath(photoTour.Name, photoTour.Id);
        var downloadFolder = Path.Combine(webHost.WebRootPath, "download");
        var zipFile = Path.Combine(downloadFolder, photoTour.Name.SanitizeFileName() + ".zip");
        if (File.Exists(zipFile)) File.Delete(zipFile);
        ZipFile.CreateFromDirectory(folder, zipFile, CompressionLevel.Fastest, true);
        Directory.CreateDirectory(downloadFolder);
        async Task DeleteFile()
        {
            await Task.Delay(TimeSpan.FromMinutes(10));
            File.Delete(zipFile);
        }
        DeleteFile().RunInBackground(ex => ex.LogError());
        return Path.Combine(IEnvironmentConfiguration.DownloadFolder, Path.GetFileName(zipFile));
    }
}
