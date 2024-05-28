using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.TemperatureMonitor;

[ApiController]
[Route("api/[controller]")]
public class TemperatureController(IDeviceApiFactory apiFactory)
{
    [HttpGet("devices")]
    public async Task<IEnumerable<string>> GetDevices(string ip)
    {
        return await apiFactory.TemperatureClient(ip).DevicesAsync();
    }
}
