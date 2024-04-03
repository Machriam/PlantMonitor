using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Shared.Features.HealthChecking;

namespace PlantMonitorControl.Features.HealthChecking;

[ApiController]
[Route("api/[controller]")]
public class HealthController(IHealthSettingsEditor healthSettings) : ControllerBase
{
    [HttpGet()]
    public DeviceHealth GetDeviceHealth()
    {
        return healthSettings.GetHealth();
    }
}