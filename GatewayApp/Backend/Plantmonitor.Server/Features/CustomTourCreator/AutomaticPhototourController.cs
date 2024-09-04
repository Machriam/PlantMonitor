using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.CustomTourCreator;

[ApiController]
[Route("api/[controller]")]
public class CustomTourController(IDeviceApiFactory deviceApi, IEnvironmentConfiguration configuration)
{
    public record struct CustomTourData(AvailableCustomData DeviceData, bool AvailableOnDevice, bool Stored);

    [HttpGet("availabletours")]
    public async Task<IEnumerable<CustomTourData>> AvailableTours(string ip)
    {
        const float inverseMb = 1f / 1024f / 1024f;
        var localDataPath = configuration.CustomTourDataPath();
        var localData = Directory.GetFiles(localDataPath).Select(f => new { FileName = Path.GetFileName(f), Size = new FileInfo(f).Length * inverseMb })
            .ToDictionary(x => x.FileName);
        var remoteData = await deviceApi.CustomTourClient(ip).AvailabledataAsync();
        var result = new Dictionary<string, CustomTourData>();
        foreach (var file in localData) result.Add(file.Key, new CustomTourData(new(file.Key, file.Value.Size), false, true));
        foreach (var file in remoteData)
        {
            if (file?.FileName == null) continue;
            if (result.TryGetValue(file.FileName, out var value)) result[file.FileName] = new(value.DeviceData, true, true);
            else result.Add(file.FileName, new CustomTourData(file, true, false));
        }
        return result.Values;
    }

    [HttpGet("downloadfromdevice")]
    public async Task DownloadFile(string ip, string fileName)
    {
        var localDataPath = Path.Combine(configuration.CustomTourDataPath(), fileName);
        if (Path.Exists(localDataPath)) File.Delete(localDataPath);
        var stream = await deviceApi.CustomTourClient(ip).DownloadfileAsync(fileName);
        using var fileStream = File.Create(localDataPath);
    }
}
