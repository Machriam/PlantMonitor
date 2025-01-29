using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Shared.Features.ImageStreaming;

namespace Plantmonitor.Server.Features.ImageStitching;

public record struct PictureSeriesData(int Count, string FolderName, CameraType? Type, DateTime Timestamp);
public record struct PictureTripData(PictureSeriesData IrData, PictureSeriesData VisData, DateTime TimeStamp, string DeviceId, long TripId);
public record struct SeriesByDevice(string DeviceId, string FolderName, DateTime FolderDate);

[ApiController]
[Route("api/[controller]")]
public class PictureController(IEnvironmentConfiguration configuration, IDeviceApiFactory deviceApi, IDataContext context)
{
    private static readonly Dictionary<string, CameraType> s_cameraTypesByEnding = Enum.GetValues<CameraType>().Cast<CameraType>()
        .Select(c => (c.Attribute<CameraTypeInfo>().FileEnding, CameraType: c))
        .ToDictionary(x => x.FileEnding, x => x.CameraType);

    [HttpGet("allpictureddevices")]
    public IEnumerable<SeriesByDevice> GetAllPicturedDevices()
    {
        foreach (var folder in configuration.PictureFolders())
        {
            var folderName = Path.GetFileName(folder);
            var split = folderName.Split('_');
            if (!Guid.TryParse(split[1], out var guid)) continue;
            yield return new(guid.ToString(), folderName, Directory.GetCreationTimeUtc(folder));
        }
    }

    [HttpPost("updateiroffset")]
    public async Task UpdateIrOffset([FromBody] IrCameraOffset offset, string ip)
    {
        await deviceApi.HealthClient(ip).UpdateiroffsetAsync(offset);
    }

    [HttpGet("pictureseriesnames")]
    public IEnumerable<PictureSeriesData> GetPictureSeries(string deviceId, DateTime fromTime)
    {
        if (fromTime == DateTime.MinValue) fromTime = DateTime.UtcNow;
        var path = configuration.PicturePath(deviceId);
        var directories = path
            .Pipe(Directory.EnumerateDirectories)
            .Select(d => Path.GetFileName(d))
            .Where(x => !x.IsEmpty());
        return directories.Select(d =>
        {
            var imageFolderTime = d.Pipe(TimeFromImageFolder);
            var files = Directory.EnumerateFiles(Path.Combine(path, d));
            var firstFile = files.FirstOrDefault();
            if (firstFile.IsEmpty() || !imageFolderTime.Success) return new PictureSeriesData(files.Count(), d, null, DateTime.MinValue);
            var ending = $".{firstFile!.Split(".").Last()}";
            return new PictureSeriesData(files.Count(), d, s_cameraTypesByEnding.TryGetValue(ending, out var type) ? type : null, imageFolderTime.Time);
        })
            .Where(d => d.Timestamp <= fromTime)
            .OrderByDescending(d => d.Timestamp)
            .Take(500);
    }

    private static (bool Success, DateTime Time) TimeFromImageFolder(string imageFolder)
    {
        imageFolder = Path.GetFileNameWithoutExtension(imageFolder) ?? "";
        return (DateTime.TryParseExact(imageFolder, CameraStreamFormatter.PictureDateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time), time);
    }

    [HttpGet("pictureseriesoftour")]
    public IEnumerable<PictureTripData> PictureSeriesOfTour(long photoTour)
    {
        var trips = context.PhotoTourTrips
            .Where(ptt => ptt.PhotoTourFk == photoTour && ptt.VisDataFolder.Length > 3).ToList();
        var deviceId = context.AutomaticPhotoTours.First(pt => pt.Id == photoTour).DeviceId.ToString();
        var directories = trips
            .Select(t => (Trip: t, IrCount: Path.Exists(t.IrDataFolder) ? Directory.GetFiles(t.IrDataFolder).Length : 0,
                          VisCount: Path.Exists(t.VisDataFolder) ? Directory.GetFiles(t.VisDataFolder).Length : 0));
        return directories.Select(d =>
        {
            var visTime = d.Trip.VisDataFolder.Pipe(TimeFromImageFolder);
            var irTime = d.Trip.VisDataFolder.Pipe(TimeFromImageFolder);
            return new PictureTripData(new(d.IrCount, d.Trip.IrDataFolder, CameraType.IR, irTime.Time),
                new(d.VisCount, d.Trip.VisDataFolder, CameraType.Vis, visTime.Time), d.Trip.Timestamp, deviceId, d.Trip.Id);
        })
            .OrderBy(d => d.TimeStamp);
    }
}
