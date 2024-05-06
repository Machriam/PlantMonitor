using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.IIS;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;

namespace Plantmonitor.Server.Features.DeviceControl;

public record struct PictureSeriesData(int Count, string FileName);

[ApiController]
[Route("api/[controller]")]
public class PictureController(IEnvironmentConfiguration configuration)
{
    [HttpGet("pictureseriesnames")]
    public IEnumerable<PictureSeriesData> GetPictureSeries(string deviceId)
    {
        var path = configuration.PicturePath(deviceId);
        var directories = Directory.EnumerateDirectories(path).Select(d => Path.GetFileName(d)).Where(x => !x.IsEmpty());
        return directories.Select(d => new PictureSeriesData(Directory.EnumerateFiles(Path.Combine(path, d)).Count(), d));
    }
}
