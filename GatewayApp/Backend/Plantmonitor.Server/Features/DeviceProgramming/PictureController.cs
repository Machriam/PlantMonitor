using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Shared.Features.ImageStreaming;

namespace Plantmonitor.Server.Features.DeviceControl;

public record struct PictureSeriesData(int Count, string FolderName, CameraType? Type);

[ApiController]
[Route("api/[controller]")]
public class PictureController(IEnvironmentConfiguration configuration)
{
    private static readonly Dictionary<string, CameraType> s_cameraTypesByEnding = Enum.GetValues<CameraType>().Cast<CameraType>()
        .Select(c => (c.Attribute<CameraTypeInfo>().FileEnding, CameraType: c))
        .ToDictionary(x => x.FileEnding, x => x.CameraType);

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
