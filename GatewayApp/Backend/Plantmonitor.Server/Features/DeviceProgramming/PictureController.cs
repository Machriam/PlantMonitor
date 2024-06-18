using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Shared.Features.ImageStreaming;

namespace Plantmonitor.Server.Features.DeviceControl;

public record struct PictureSeriesData(int Count, string FolderName, CameraType? Type);
public record struct SeriesByDevice(string DeviceId, string FolderName);

[ApiController]
[Route("api/[controller]")]
public class PictureController(IEnvironmentConfiguration configuration, IDeviceApiFactory deviceApi)
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
            yield return new(guid.ToString(), folderName);
        }
    }

    [HttpPost("updateiroffset")]
    public async Task UpdateIrOffset([FromBody] IrCameraOffset offset, string ip)
    {
        await deviceApi.HealthClient(ip).UpdateiroffsetAsync(offset);
    }

    [HttpGet("pictureseriesnames")]
    public IEnumerable<PictureSeriesData> GetPictureSeries(string deviceId)
    {
        var path = configuration.PicturePath(deviceId);
        var directories = Directory.EnumerateDirectories(path).Select(d => Path.GetFileName(d)).Where(x => !x.IsEmpty());
        return directories.Select(d =>
        {
            var files = Directory.EnumerateFiles(Path.Combine(path, d));
            var firstFile = files.FirstOrDefault();
            if (firstFile.IsEmpty()) return new PictureSeriesData(files.Count(), d, null);
            var ending = $".{firstFile!.Split(".").Last()}";
            return new PictureSeriesData(files.Count(), d, s_cameraTypesByEnding.TryGetValue(ending, out var type) ? type : null);
        });
    }
}
