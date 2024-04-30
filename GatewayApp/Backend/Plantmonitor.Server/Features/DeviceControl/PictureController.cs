using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.IIS;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;

namespace Plantmonitor.Server.Features.DeviceControl;

[ApiController]
[Route("api/[controller]")]
public class PictureController(IEnvironmentConfiguration configuration)
{
    [HttpGet("pictureseriesnames")]
    public IEnumerable<string> GetPictureSeries(string deviceId)
    {
        var path = configuration.PicturePath(deviceId);
        return Directory.EnumerateDirectories(path).Select(d => Path.GetFileName(d)).Where(x => !x.IsEmpty());
    }
}
