using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Shared.Features.ImageStreaming;

namespace Plantmonitor.Server.Features.DeviceControl;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(IDataContext context, IEnvironmentConfiguration configuration)
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
}
