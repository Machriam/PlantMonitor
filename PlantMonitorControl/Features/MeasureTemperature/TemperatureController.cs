using Microsoft.AspNetCore.Mvc;

namespace PlantMonitorControl.Features.MeasureTemperature;

[ApiController]
[Route("api/[controller]")]
public class TemperatureController(IClick2TempInterop clickInterop) : ControllerBase
{
    [HttpGet("devices")]
    public IEnumerable<string> GetDevices()
    {
        return clickInterop.GetDevices();
    }

    [HttpGet("stopmeasuring")]
    public void StopMeasuring()
    {
        clickInterop.StopRunning();
    }
}
